using Xunit;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using System.Net.Http;
using System.Net.Http.Json;
using System.Threading.Tasks;
using System.Collections.Generic;
using System.IO;
using Task.Core;
using Task.Core.Auth;
using Task.Core.Providers.Telegram;

namespace Task.Cli.Tests.IntegrationTests
{
	public class ApiIntegrationTests : IClassFixture<TestWebApplicationFactory>
	{
		private readonly TestWebApplicationFactory _factory;
		private readonly HttpClient _client;

		public ApiIntegrationTests(TestWebApplicationFactory factory)
		{
			_factory = factory;
			_factory.ClearDatabase();
			_client = _factory.CreateUserClient();
		}

		[Fact]
		public async System.Threading.Tasks.Task GetTasks_ReturnsEmptyList_WhenNoTasks()
		{
			var response = await _client.GetAsync("/api/tasks");
			response.EnsureSuccessStatusCode();
			var tasks = await response.Content.ReadFromJsonAsync<List<TaskDto>>();
			Assert.NotNull(tasks);
			Assert.Empty(tasks);
		}

		[Fact]
		public async System.Threading.Tasks.Task CreateTask_ReturnsCreatedTask()
		{
			var newTask = new TaskCreateDto
			{
				Title = "Test Task",
				Description = "Test Description",
				Priority = "high"
			};

			var response = await _client.PostAsJsonAsync("/api/tasks", newTask);
			response.EnsureSuccessStatusCode();
			var task = await response.Content.ReadFromJsonAsync<TaskDto>();
			Assert.NotNull(task);

			Assert.Equal("Test Task", task.Title);
			Assert.Equal("Test Description", task.Description);
			Assert.Equal("high", task.Priority);
			Assert.Equal("todo", task.Status);
		}

		[Fact]
		public async System.Threading.Tasks.Task CreateTask_WithBlockedStatus_ReturnsBlockReason()
		{
			var newTask = new TaskCreateDto
			{
				Title = "Blocked Task",
				Description = "Needs access",
				Priority = "high",
				Status = "blocked",
				BlockReason = "Waiting on API key"
			};

			var response = await _client.PostAsJsonAsync("/api/tasks", newTask);
			response.EnsureSuccessStatusCode();
			var task = await response.Content.ReadFromJsonAsync<TaskDto>();
			Assert.NotNull(task);

			Assert.Equal("blocked", task.Status);
			Assert.Equal("Waiting on API key", task.BlockReason);
		}

		[Fact]
		public async System.Threading.Tasks.Task GetTask_ReturnsTask_WhenExists()
		{
			var newTask = new TaskCreateDto { Title = "Get Test", Priority = "medium" };
			var createResponse = await _client.PostAsJsonAsync("/api/tasks", newTask);
			var createdTask = await createResponse.Content.ReadFromJsonAsync<TaskDto>();
			Assert.NotNull(createdTask);

			var getResponse = await _client.GetAsync($"/api/tasks/{createdTask.Uid}");
			getResponse.EnsureSuccessStatusCode();
			var retrievedTask = await getResponse.Content.ReadFromJsonAsync<TaskDto>();
			Assert.NotNull(retrievedTask);

			Assert.Equal(createdTask.Uid, retrievedTask.Uid);
			Assert.Equal("Get Test", retrievedTask.Title);
		}

		[Fact]
		public async System.Threading.Tasks.Task UpdateTask_UpdatesExistingTask()
		{
			var newTask = new TaskCreateDto { Title = "Original", Priority = "low" };
			var createResponse = await _client.PostAsJsonAsync("/api/tasks", newTask);
			var createdTask = await createResponse.Content.ReadFromJsonAsync<TaskDto>();
			Assert.NotNull(createdTask);

			var updateDto = new TaskUpdateDto
			{
				Title = "Updated",
				Priority = "high"
			};
			var updateResponse = await _client.PutAsJsonAsync($"/api/tasks/{createdTask.Uid}", updateDto);
			updateResponse.EnsureSuccessStatusCode();

			var getResponse = await _client.GetAsync($"/api/tasks/{createdTask.Uid}");
			var updatedTask = await getResponse.Content.ReadFromJsonAsync<TaskDto>();
			Assert.NotNull(updatedTask);
			Assert.Equal("Updated", updatedTask.Title);
			Assert.Equal("high", updatedTask.Priority);
		}

		[Fact]
		public async System.Threading.Tasks.Task DeleteTask_RemovesTask()
		{
			var newTask = new TaskCreateDto { Title = "To Delete", Priority = "medium" };
			var createResponse = await _client.PostAsJsonAsync("/api/tasks", newTask);
			var createdTask = await createResponse.Content.ReadFromJsonAsync<TaskDto>();
			Assert.NotNull(createdTask);

			var deleteResponse = await _client.DeleteAsync($"/api/tasks/{createdTask.Uid}");
			deleteResponse.EnsureSuccessStatusCode();

			var getResponse = await _client.GetAsync($"/api/tasks/{createdTask.Uid}");
			Assert.Equal(System.Net.HttpStatusCode.NotFound, getResponse.StatusCode);
		}

		[Fact]
		public async System.Threading.Tasks.Task CompleteTask_SetsStatusToCompleted()
		{
			var newTask = new TaskCreateDto { Title = "To Complete", Priority = "medium" };
			var createResponse = await _client.PostAsJsonAsync("/api/tasks", newTask);
			var createdTask = await createResponse.Content.ReadFromJsonAsync<TaskDto>();
			Assert.NotNull(createdTask);

			var completeResponse = await _client.PatchAsync($"/api/tasks/{createdTask.Uid}/complete", null);
			completeResponse.EnsureSuccessStatusCode();

			var getResponse = await _client.GetAsync($"/api/tasks/{createdTask.Uid}");
			var completedTask = await getResponse.Content.ReadFromJsonAsync<TaskDto>();
			Assert.NotNull(completedTask);
			Assert.Equal("done", completedTask.Status);
		}

		[Fact]
		public async System.Threading.Tasks.Task SearchTasks_ReturnsMatchingTasks()
		{
			await _client.PostAsJsonAsync("/api/tasks", new TaskCreateDto { Title = "Buy groceries", Priority = "medium" });
			await _client.PostAsJsonAsync("/api/tasks", new TaskCreateDto { Title = "Clean house", Priority = "medium" });

			var searchResponse = await _client.GetAsync("/api/tasks/search?q=groceries");
			searchResponse.EnsureSuccessStatusCode();
			var results = await searchResponse.Content.ReadFromJsonAsync<List<TaskDto>>();
			Assert.NotNull(results);

			Assert.Single(results);
			Assert.Equal("Buy groceries", results[0].Title);
		}

		[Fact]
		public async System.Threading.Tasks.Task GetTask_ReturnsNotFound_WhenTaskDoesNotExist()
		{
			var response = await _client.GetAsync("/api/tasks/nonexistent");
			Assert.Equal(System.Net.HttpStatusCode.NotFound, response.StatusCode);
		}
	}

	public class TestWebApplicationFactory : WebApplicationFactory<Task.Api.Program>
	{
		private readonly string _testDbPath;
		private Database? _database;

		public TestWebApplicationFactory()
		{
			_testDbPath = Path.Combine(Path.GetTempPath(), $"test_tasks_{Guid.NewGuid()}.db");
		}

		public string TestDbPath => _testDbPath;

		public Database Database => _database ?? throw new InvalidOperationException("Factory not initialized.");

		public void ClearDatabase()
		{
			if (_database != null)
			{
				_database.ClearAllTasksAsync().GetAwaiter().GetResult();
				_database.ClearAuthTablesAsync().GetAwaiter().GetResult();
			}
		}

		/// <summary>Signs up + logs in a fresh user; returns a cookie-authenticated client.</summary>
		public HttpClient CreateUserClient(string? username = null)
		{
			var client = CreateClient();
			username ??= $"user_{Guid.NewGuid():N}"[..15];
			const string password = "password123";

			var signup = client.PostAsJsonAsync("/api/auth/signup", new { username, password }).GetAwaiter().GetResult();
			signup.EnsureSuccessStatusCode();

			var login = client.PostAsJsonAsync("/api/auth/login", new { username, password }).GetAwaiter().GetResult();
			login.EnsureSuccessStatusCode();
			return client;
		}

		/// <summary>Signs up a user and returns the plaintext API key for CLI configuration.</summary>
		public string CreateUserApiKey(string? username = null)
		{
			var userClient = CreateUserClient(username);
			var response = userClient.PostAsJsonAsync("/api/keys", new { name = "test-key" }).GetAwaiter().GetResult();
			response.EnsureSuccessStatusCode();

			var body = response.Content.ReadFromJsonAsync<KeyResponse>().GetAwaiter().GetResult()
				?? throw new InvalidOperationException("Key creation returned no body.");
			return body.Key ?? throw new InvalidOperationException("Key creation returned no plaintext.");
		}

		public sealed class KeyResponse
		{
			public string? Key { get; set; }
		}

		/// <summary>No-op Telegram provider: records nothing, never touches the network.</summary>
		public sealed class FakeTelegramProvider : ITelegramProvider
		{
			public System.Threading.Tasks.Task SendMessageAsync(string message, System.Threading.CancellationToken cancellationToken = default)
			{
				return System.Threading.Tasks.Task.CompletedTask;
			}
		}

		protected override void ConfigureWebHost(IWebHostBuilder builder)
		{
			builder.UseEnvironment("Testing");

			// Tests must be hermetic: never let the app call the real Telegram API.
			// (Credentials are often present in the environment, which previously made
			// GET /api/tasks on an empty board await a live api.telegram.org POST and
			// flake with 500s / 100s stalls / client aborts.)
			builder.UseSetting("Telegram:Enabled", "false");

			_database = new Database(_testDbPath);
			_database.Initialize();
			builder.ConfigureServices(services =>
			{
				var authServiceDescriptors = services
					.Where(descriptor => descriptor.ServiceType == typeof(IAuthService))
					.ToList();
				foreach (var descriptor in authServiceDescriptors)
				{
					services.Remove(descriptor);
				}

				ReplaceTelegramWithFake(services);

				services.AddSingleton(_database);
				services.AddSingleton<IAuthService>(new AuthService(_database!));
			});

			Console.WriteLine($"[TestHost] sqlite db={_testDbPath} telegram=disabled(fake)");
		}

		/// <summary>
		/// Replaces the app's real Telegram provider + notification service with a
		/// no-op fake so tests never perform live network calls.
		/// </summary>
		private static void ReplaceTelegramWithFake(IServiceCollection services)
		{
			var telegramProviderDescriptors = services
				.Where(descriptor => descriptor.ServiceType == typeof(ITelegramProvider))
				.ToList();
			foreach (var descriptor in telegramProviderDescriptors)
			{
				services.Remove(descriptor);
			}

			var notificationServiceDescriptors = services
				.Where(descriptor => descriptor.ServiceType == typeof(TelegramNotificationService))
				.ToList();
			foreach (var descriptor in notificationServiceDescriptors)
			{
				services.Remove(descriptor);
			}

			services.AddSingleton<ITelegramProvider>(new FakeTelegramProvider());
			services.AddSingleton<TelegramNotificationService>(sp => new TelegramNotificationService(
				sp.GetRequiredService<ITelegramProvider>(),
				Options.Create(new TelegramProviderOptions { Enabled = false }),
				NullLogger<TelegramNotificationService>.Instance));
		}

		protected override void Dispose(bool disposing)
		{
			base.Dispose(disposing);
			if (File.Exists(_testDbPath))
			{
				File.Delete(_testDbPath);
			}
		}
	}
}
