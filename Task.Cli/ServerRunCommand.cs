using Spectre.Console;
using Spectre.Console.Cli;
using System;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Task.Api;
using Task.Core;

namespace Task.Cli
{
	[Description("Run the Task API server in the foreground.")]
	public sealed class ServerRunCommand : AsyncCommand<ServerRunCommand.Settings>
	{
		private const string ReadySignal = "Task.Ready";

		public sealed class Settings : CommandSettings
		{
			[CommandOption("--urls <URLS>")]
			[Description("Override server URLs (e.g., http://localhost:8080). Explicit configuration disables port auto-selection.")]
			public string? Urls { get; set; }

			[CommandOption("--database-provider <PROVIDER>")]
			[Description("Database provider for the API server (sqlite or pg). Default: sqlite.")]
			public string? DatabaseProvider { get; set; }

			[CommandOption("--database-path <PATH>")]
			[Description("SQLite database path for the API server (default: config dir tasks.db, e.g. ~/.config/task/tasks.db).")]
			public string? DatabasePath { get; set; }

			[CommandOption("--pg-connection-string <VALUE>")]
			[Description("PostgreSQL connection string for the API server when --database-provider pg is used.")]
			public string? PostgresConnectionString { get; set; }

			[CommandOption("--ready-file <PATH>")]
			[Description("Write readiness details to this file once the server is ready.")]
			public string? ReadyFile { get; set; }
		}

		public override async Task<int> ExecuteAsync(CommandContext context, Settings settings, CancellationToken cancellationToken)
		{
			var config = Config.Load();
			var connectionSettings = DatabaseConnectionSettings.Create(
				provider: settings.DatabaseProvider ?? config.Database?.Provider,
				sqliteDatabasePath: settings.DatabasePath ?? config.Database?.Sqlite?.Path,
				postgresConnectionString: settings.PostgresConnectionString ?? config.Database?.Postgres?.ConnectionString);

			var options = new ApiHostOptions
			{
				Urls = settings.Urls,
				DatabaseProvider = connectionSettings.Provider,
				DatabasePath = connectionSettings.SqliteDatabasePath,
				PostgresConnectionString = connectionSettings.PostgresConnectionString,
				ApplicationName = typeof(ApiHost).Assembly.GetName().Name,
				ContentRootPath = AppContext.BaseDirectory,
				WebRootPath = Path.Combine(AppContext.BaseDirectory, "wwwroot")
			};

			var args = context.Remaining.Raw.ToArray();
			await using var handle = await ApiHost.StartAsync(args, options, cancellationToken);

			var state = new ServerState
			{
				ProcessId = Process.GetCurrentProcess().Id,
				Url = handle.Result.Url,
				Port = handle.Result.Port,
				Reason = handle.Result.Reason,
				BinaryPath = Environment.ProcessPath,
				StartedAt = DateTimeOffset.UtcNow
			};

			ServerStateStore.Save(state);
			SaveApiUrl(handle.Result.Url);

			var readiness = new
			{
				status = "ready",
				port = handle.Result.Port,
				url = handle.Result.Url,
				reason = handle.Result.Reason,
				databaseProvider = connectionSettings.Provider
			};

			if (!string.IsNullOrWhiteSpace(settings.ReadyFile))
			{
#pragma warning disable IL2026
				var payload = System.Text.Json.JsonSerializer.Serialize(readiness, JsonHelper.Options);
#pragma warning restore IL2026
				await File.WriteAllTextAsync(settings.ReadyFile, payload, cancellationToken);
			}

			if (settings.ReadyFile == null)
			{
				Console.WriteLine($"{ReadySignal} port={handle.Result.Port} url={handle.Result.Url} reason={handle.Result.Reason} provider={connectionSettings.Provider}");
				AnsiConsole.MarkupLine($"[green]Server ready[/] url={handle.Result.Url} port={handle.Result.Port} reason={handle.Result.Reason} provider={connectionSettings.Provider}");
			}

			try
			{
				await handle.WaitForShutdownAsync(cancellationToken);
				return 0;
			}
			finally
			{
				ServerStateStore.Clear(state);
			}
		}

		private static void SaveApiUrl(string url)
		{
			var config = Config.Load();
			config.ApiUrl = url;
			config.Save();
		}
	}
}
