using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Net.Http;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading;
using Npgsql;
using Task.Core;
using Testcontainers.PostgreSql;
using Xunit;
using Xunit.Sdk;

namespace Task.Cli.Tests.IntegrationTests;

public sealed class PostgreSqlCliEndToEndTests : IClassFixture<PostgreSqlCliFixture>
{
	private static readonly JsonSerializerOptions JsonOptions = new()
	{
		PropertyNameCaseInsensitive = true
	};

	private readonly PostgreSqlCliFixture _fixture;

	public PostgreSqlCliEndToEndTests(PostgreSqlCliFixture fixture)
	{
		_fixture = fixture;
	}

	[Fact]
	public async System.Threading.Tasks.Task AddAndListWorkflow_UsesPostgreSqlBackedServerViaConfigPath()
	{
		var addResult = await _fixture.RunCliCommandAsync(
			new[]
			{
				"add",
				"--title", "PostgreSQL-backed CLI task",
				"--description", "end-to-end validation",
				"--priority", "high",
				"--project", "pg-e2e",
				"--json"
			},
			TimeSpan.FromSeconds(30));

		Assert.True(
			addResult.ExitCode == 0,
			$"Expected add command to succeed. ExitCode={addResult.ExitCode}\nSTDOUT:\n{addResult.StandardOutput}\nSTDERR:\n{addResult.StandardError}");

		var createdTask = DeserializeJson<TaskItem>(addResult.StandardOutput);
		Assert.NotNull(createdTask);
		Assert.Equal("PostgreSQL-backed CLI task", createdTask!.Title);
		Assert.Equal("pg-e2e", createdTask.Project);
		Assert.Equal("high", createdTask.Priority);
		Assert.Equal("todo", createdTask.Status);

		var listResult = await _fixture.RunCliCommandAsync(
			new[] { "list", "--project", "pg-e2e", "--json" },
			TimeSpan.FromSeconds(30));

		Assert.True(
			listResult.ExitCode == 0,
			$"Expected list command to succeed. ExitCode={listResult.ExitCode}\nSTDOUT:\n{listResult.StandardOutput}\nSTDERR:\n{listResult.StandardError}");

		var tasks = DeserializeJson<List<TaskItem>>(listResult.StandardOutput);
		Assert.NotNull(tasks);
		Assert.Single(tasks!);
		Assert.Equal(createdTask.Uid, tasks[0].Uid);
		Assert.Equal("pg", _fixture.Readiness.DatabaseProvider);
	}

	private static T DeserializeJson<T>(string json)
	{
		var value = JsonSerializer.Deserialize<T>(json, JsonOptions);
		if (value == null)
		{
			throw new XunitException($"Failed to deserialize CLI JSON output to {typeof(T).Name}. Payload:\n{json}");
		}

		return value;
	}
}

public sealed class PostgreSqlCliFixture : IAsyncLifetime
{
	private static readonly string CliAssemblyPath = ResolveCliAssemblyPath();
	private readonly string _tempRoot = Path.Combine(Path.GetTempPath(), $"task-pg-cli-tests-{Guid.NewGuid():N}");
	private readonly string _homeDirectory;
	private readonly string _configHomeDirectory;
	private readonly string _configFilePath;
	private readonly string _readyFilePath;
	private readonly PostgreSqlContainer _container;
	private Process? _serverProcess;

	public PostgreSqlCliFixture()
	{
		_homeDirectory = Path.Combine(_tempRoot, "home");
		_configHomeDirectory = Path.Combine(_tempRoot, "config-home");
		_configFilePath = Path.Combine(_configHomeDirectory, "task", "config.json");
		_readyFilePath = Path.Combine(_tempRoot, "ready.json");
		_container = new PostgreSqlBuilder("postgres:16.4")
			.WithDatabase("task_tests")
			.WithUsername("task")
			.WithPassword("task")
			.Build();
	}

	public ReadyFilePayload Readiness { get; private set; } = null!;

	public async System.Threading.Tasks.Task InitializeAsync()
	{
		Directory.CreateDirectory(_homeDirectory);
		Directory.CreateDirectory(_configHomeDirectory);

		await _container.StartAsync();
		Console.WriteLine(
			$"[PostgreSqlCliFixture] container={_container.Id} host={_container.Hostname}:{_container.GetMappedPublicPort(5432)} config={_configFilePath} readyFile={_readyFilePath}");
		await WriteConfigAsync();

		_serverProcess = StartCliProcess(
			new[] { "server", "run", "--ready-file", _readyFilePath },
			_homeDirectory,
			_configHomeDirectory);

		Readiness = await WaitForReadyAsync(_serverProcess, _readyFilePath, TimeSpan.FromSeconds(60));
		await WaitForApiUrlInConfigAsync(TimeSpan.FromSeconds(30));
		await ResetDatabaseAsync();
		await ProvisionUserAndKeyAsync();
	}

	public async System.Threading.Tasks.Task DisposeAsync()
	{
		await StopServerAsync();
		await _container.DisposeAsync();

		if (Directory.Exists(_tempRoot))
		{
			Directory.Delete(_tempRoot, recursive: true);
		}
	}

	public async System.Threading.Tasks.Task ResetDatabaseAsync()
	{
		await using var connection = new NpgsqlConnection(_container.GetConnectionString());
		await connection.OpenAsync();

		await using var command = connection.CreateCommand();
		command.CommandText = "TRUNCATE TABLE tasks, users, api_keys RESTART IDENTITY CASCADE";
		await command.ExecuteNonQueryAsync();
	}

	/// <summary>
	/// The server requires authentication. Signs up a user, creates an API key,
	/// and stores it in the CLI config so subsequent `task` commands authenticate.
	/// </summary>
	private async System.Threading.Tasks.Task ProvisionUserAndKeyAsync()
	{
		var config = JsonSerializer.Deserialize<JsonElement>(await File.ReadAllTextAsync(_configFilePath));
		var apiUrl = TryGetApiUrl(config, out var url) ? url : null;
		if (string.IsNullOrWhiteSpace(apiUrl))
		{
			throw new XunitException("CLI config has no apiUrl; cannot provision a user.");
		}

		// Sign up, then sign in to establish a session cookie (the API's signup does not
		// create a session; keys are per-user and require authentication).
		using var cookieHandler = new HttpClientHandler();
		using var client = new HttpClient(cookieHandler);
		var signup = await client.PostAsJsonAsync($"{apiUrl}/api/auth/signup", new { username = "e2e-user", password = "password123" });
		if (!signup.IsSuccessStatusCode)
		{
			var body = await signup.Content.ReadAsStringAsync();
			throw new XunitException($"Signup failed: {(int)signup.StatusCode} {body} (url={apiUrl})");
		}

		var login = await client.PostAsJsonAsync($"{apiUrl}/api/auth/login", new { username = "e2e-user", password = "password123" });
		if (!login.IsSuccessStatusCode)
		{
			var body = await login.Content.ReadAsStringAsync();
			throw new XunitException($"Login failed: {(int)login.StatusCode} {body} (url={apiUrl})");
		}

		var keyResponse = await client.PostAsJsonAsync($"{apiUrl}/api/keys", new { name = "e2e" });
		keyResponse.EnsureSuccessStatusCode();
		using var keyDocument = JsonDocument.Parse(await keyResponse.Content.ReadAsStringAsync());
		var key = keyDocument.RootElement.GetProperty("key").GetString()
			?? throw new XunitException("Key creation returned no plaintext.");

		// Persist the key into the CLI config (mirrors `task config set api.key <key>`).
		var configObject = JsonSerializer.Deserialize<Dictionary<string, object?>>(await File.ReadAllTextAsync(_configFilePath))
			?? new Dictionary<string, object?>();
		configObject["ApiKey"] = key;
		await File.WriteAllTextAsync(
			_configFilePath,
			JsonSerializer.Serialize(configObject, new JsonSerializerOptions { WriteIndented = true }));
	}

	public async System.Threading.Tasks.Task<CliCommandResult> RunCliCommandAsync(string[] arguments, TimeSpan timeout)
	{
		using var process = new Process
		{
			StartInfo = CreateStartInfo(arguments, _homeDirectory, _configHomeDirectory, redirectOutput: true)
		};

		if (!process.Start())
		{
			throw new XunitException("Failed to start task CLI command.");
		}

		var stdoutTask = process.StandardOutput.ReadToEndAsync();
		var stderrTask = process.StandardError.ReadToEndAsync();

		using var cts = new CancellationTokenSource(timeout);
		try
		{
			await process.WaitForExitAsync(cts.Token);
		}
		catch (OperationCanceledException)
		{
			// Leave no orphan processes behind and surface captured output so a
			// hung CLI command can be diagnosed from the failure message alone.
			try
			{
				process.Kill(entireProcessTree: true);
			}
			catch
			{
			}

			await process.WaitForExitAsync();
			throw new XunitException(
				$"CLI command timed out after {timeout.TotalSeconds}s: {string.Join(' ', arguments)}\nSTDOUT:\n{await stdoutTask}\nSTDERR:\n{await stderrTask}");
		}

		return new CliCommandResult(
			process.ExitCode,
			await stdoutTask,
			await stderrTask);
	}

	private async System.Threading.Tasks.Task WriteConfigAsync()
	{
		Directory.CreateDirectory(Path.GetDirectoryName(_configFilePath)!);

		var config = new
		{
			DefaultOutput = "plain",
			Database = new
			{
				Provider = "pg",
				Sqlite = new
				{
					Path = string.Empty
				},
				Postgres = new
				{
					ConnectionString = _container.GetConnectionString()
				}
			}
		};

		await File.WriteAllTextAsync(
			_configFilePath,
			JsonSerializer.Serialize(config, new JsonSerializerOptions { WriteIndented = true }));
	}

	private async System.Threading.Tasks.Task WaitForApiUrlInConfigAsync(TimeSpan timeout)
	{
		using var cts = new CancellationTokenSource(timeout);

		while (!cts.IsCancellationRequested)
		{
			if (File.Exists(_configFilePath))
			{
				try
				{
					var json = await File.ReadAllTextAsync(_configFilePath, cts.Token);
					using var document = JsonDocument.Parse(json);
					if (TryGetApiUrl(document.RootElement, out var apiUrl)
						&& !string.IsNullOrWhiteSpace(apiUrl))
					{
						return;
					}
				}
				catch (IOException)
				{
				}
				catch (JsonException)
				{
				}
			}

			await System.Threading.Tasks.Task.Delay(TimeSpan.FromMilliseconds(200), cts.Token);
		}

		throw new XunitException($"Timed out waiting for CLI config apiUrl update at {_configFilePath}");
	}

	private static bool TryGetApiUrl(JsonElement root, out string? apiUrl)
	{
		if (root.TryGetProperty("ApiUrl", out var pascalCaseValue))
		{
			apiUrl = pascalCaseValue.GetString();
			return true;
		}

		if (root.TryGetProperty("apiUrl", out var camelCaseValue))
		{
			apiUrl = camelCaseValue.GetString();
			return true;
		}

		apiUrl = null;
		return false;
	}

	private async System.Threading.Tasks.Task StopServerAsync()
	{
		if (_serverProcess == null || _serverProcess.HasExited)
		{
			return;
		}

		try
		{
			await RunCliCommandAsync(new[] { "server", "stop" }, TimeSpan.FromSeconds(30));
		}
		catch
		{
		}

		if (!_serverProcess.HasExited)
		{
			try
			{
				using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(15));
				await _serverProcess.WaitForExitAsync(cts.Token);
			}
			catch (OperationCanceledException)
			{
				_serverProcess.Kill(entireProcessTree: true);
				await _serverProcess.WaitForExitAsync();
			}
		}
	}

	private static string ResolveCliAssemblyPath()
	{
		var repositoryRoot = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "../../../../"));
		var cliAssemblyPath = Path.Combine(repositoryRoot, "Task.Cli", "bin", "Debug", "net10.0", "linux-x64", "Task.Cli.dll");

		if (!File.Exists(cliAssemblyPath))
		{
			throw new XunitException($"Task CLI assembly not found at expected path: {cliAssemblyPath}");
		}

		return cliAssemblyPath;
	}

	private static Process StartCliProcess(string[] arguments, string homeDirectory, string configHome)
	{
		var process = new Process
		{
			StartInfo = CreateStartInfo(arguments, homeDirectory, configHome, redirectOutput: true),
			EnableRaisingEvents = true
		};

		if (!process.Start())
		{
			throw new XunitException("Failed to start task CLI process.");
		}

		return process;
	}

	private static async System.Threading.Tasks.Task<ReadyFilePayload> WaitForReadyAsync(Process serverProcess, string readyFile, TimeSpan timeout)
	{
		using var cts = new CancellationTokenSource(timeout);

		while (!cts.IsCancellationRequested)
		{
			if (serverProcess.HasExited)
			{
				var stdout = await serverProcess.StandardOutput.ReadToEndAsync();
				var stderr = await serverProcess.StandardError.ReadToEndAsync();
				throw new XunitException(
					$"Foreground server exited before writing readiness details. ExitCode={serverProcess.ExitCode}\nSTDOUT:\n{stdout}\nSTDERR:\n{stderr}");
			}

			if (File.Exists(readyFile))
			{
				try
				{
					var json = await File.ReadAllTextAsync(readyFile, cts.Token);
					var payload = JsonSerializer.Deserialize<ReadyFilePayload>(json, new JsonSerializerOptions
					{
						PropertyNameCaseInsensitive = true
					});

					if (payload != null && !string.IsNullOrWhiteSpace(payload.Url))
					{
						return payload;
					}
				}
				catch (IOException)
				{
				}
				catch (JsonException)
				{
				}
			}

			await System.Threading.Tasks.Task.Delay(TimeSpan.FromMilliseconds(200), cts.Token);
		}

		throw new XunitException($"Timed out waiting for foreground server readiness file: {readyFile}");
	}

	private static ProcessStartInfo CreateStartInfo(string[] arguments, string homeDirectory, string configHome, bool redirectOutput)
	{
		var startInfo = new ProcessStartInfo
		{
			FileName = "dotnet",
			WorkingDirectory = Path.GetDirectoryName(CliAssemblyPath)!,
			UseShellExecute = false,
			CreateNoWindow = true,
			RedirectStandardOutput = redirectOutput,
			RedirectStandardError = redirectOutput
		};

		startInfo.ArgumentList.Add(CliAssemblyPath);
		foreach (var argument in arguments)
		{
			startInfo.ArgumentList.Add(argument);
		}

		startInfo.Environment["HOME"] = homeDirectory;
		startInfo.Environment["XDG_CONFIG_HOME"] = configHome;
		startInfo.Environment["DOTNET_CLI_HOME"] = homeDirectory;
		startInfo.Environment["NO_COLOR"] = "1";

		// Tests must be hermetic: strip Telegram credentials inherited from the parent
		// environment so the spawned API server never calls the live Telegram API.
		startInfo.Environment.Remove("Telegram__BotToken");
		startInfo.Environment.Remove("Telegram__ChatId");
		startInfo.Environment.Remove("Telegram__Enabled");

		return startInfo;
	}

	public sealed record CliCommandResult(int ExitCode, string StandardOutput, string StandardError);

	public sealed class ReadyFilePayload
	{
		[JsonPropertyName("status")]
		public string Status { get; set; } = string.Empty;

		[JsonPropertyName("port")]
		public int Port { get; set; }

		[JsonPropertyName("url")]
		public string Url { get; set; } = string.Empty;

		[JsonPropertyName("reason")]
		public string Reason { get; set; } = string.Empty;

		[JsonPropertyName("databaseProvider")]
		public string DatabaseProvider { get; set; } = string.Empty;
	}
}
