using Xunit;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.DependencyInjection;
using System.Net;
using System.Net.Http;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading.Tasks;
using System.Collections.Generic;
using System.IO;
using System.Net.Http;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Task.Core;
using Task.Core.Providers.Telegram;
using Xunit;
using SystemTask = System.Threading.Tasks.Task;

namespace Task.Api.Tests.IntegrationTests
{
	public class ApiIntegrationTests : IClassFixture<TestWebApplicationFactory>
	{
		private readonly TestWebApplicationFactory _factory;
		private readonly HttpClient _client;

		public ApiIntegrationTests(TestWebApplicationFactory factory)
		{
			_factory = factory;
			_client = _factory.CreateClient();
			_factory.ClearDatabase();
		}

		[Fact]
		public async SystemTask GetTasks_ReturnsEmptyList_WhenNoTasks()
		{
			var response = await _client.GetAsync("/api/tasks");
			response.EnsureSuccessStatusCode();
			var tasks = await response.Content.ReadFromJsonAsync<List<TaskDto>>();
			Assert.NotNull(tasks);
			Assert.Empty(tasks);
		}

		[Fact]
		public async SystemTask CreateTask_ReturnsCreatedTask()
		{
			var newTask = new TaskCreateDto
			{
				Title = "Test Task",
				Description = "Test Description",
				Priority = "high",
				Assignee = "john.doe"
			};

			var response = await _client.PostAsJsonAsync("/api/tasks", newTask);
			response.EnsureSuccessStatusCode();
			var task = await response.Content.ReadFromJsonAsync<TaskDto>();
			Assert.NotNull(task);

			Assert.Equal("Test Task", task.Title);
			Assert.Equal("Test Description", task.Description);
			Assert.Equal("high", task.Priority);
			Assert.Equal("john.doe", task.Assignee);
			Assert.Equal("todo", task.Status);
		}

		[Fact]
		public async SystemTask GetTask_ReturnsTask_WhenExists()
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
		public async SystemTask UpdateTask_UpdatesExistingTask()
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
		public async SystemTask DeleteTask_RemovesTask()
		{
			var newTask = new TaskCreateDto { Title = "To Delete", Priority = "medium" };
			var createResponse = await _client.PostAsJsonAsync("/api/tasks", newTask);
			var createdTask = await createResponse.Content.ReadFromJsonAsync<TaskDto>();
			Assert.NotNull(createdTask);

			var deleteResponse = await _client.DeleteAsync($"/api/tasks/{createdTask.Uid}");
			deleteResponse.EnsureSuccessStatusCode();

			var getResponse = await _client.GetAsync($"/api/tasks/{createdTask.Uid}");
			Assert.Equal(HttpStatusCode.NotFound, getResponse.StatusCode);
		}

		[Fact]
		public async SystemTask CompleteTask_SetsStatusToCompleted()
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
		public async SystemTask SearchTasks_ReturnsMatchingTasks()
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
		public async SystemTask GetTask_ReturnsNotFound_WhenTaskDoesNotExist()
		{
			var response = await _client.GetAsync("/api/tasks/nonexistent");
			Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
		}

		[Fact]
		public async System.Threading.Tasks.Task GetTasks_FiltersByMultipleFields()
		{
			var match = await CreateTaskAsync(new TaskCreateDto
			{
				Title = "Match",
				Description = "Focus",
				Priority = "high",
				DueDate = new DateTime(2024, 1, 10),
				Tags = new List<string> { "backend", "urgent" },
				Project = "alpha",
				Assignee = "alex"
			});
			await _client.PutAsJsonAsync($"/api/tasks/{match.Uid}", new TaskUpdateDto { Status = "in_progress" });

			await CreateTaskAsync(new TaskCreateDto
			{
				Title = "Other",
				Priority = "low",
				DueDate = new DateTime(2024, 2, 10),
				Tags = new List<string> { "frontend" },
				Project = "beta",
				Assignee = "sam"
			});

			var query = "/api/tasks?status=in_progress&priority=high&project=alpha&assignee=alex&tags=backend&dueBefore=2024-01-31&dueAfter=2024-01-01";
			var response = await _client.GetAsync(query);
			response.EnsureSuccessStatusCode();
			var results = await response.Content.ReadFromJsonAsync<List<TaskDto>>();
			Assert.NotNull(results);
			Assert.Single(results);
			Assert.Equal("Match", results[0].Title);
		}

		[Fact]
		public async System.Threading.Tasks.Task GetTasks_SortsAndPaginates()
		{
			await CreateTaskAsync(new TaskCreateDto { Title = "Bravo", Priority = "medium" });
			await CreateTaskAsync(new TaskCreateDto { Title = "Alpha", Priority = "medium" });
			await CreateTaskAsync(new TaskCreateDto { Title = "Charlie", Priority = "medium" });

			var response = await _client.GetAsync("/api/tasks?sortBy=title&sortOrder=asc&offset=1&limit=1");
			response.EnsureSuccessStatusCode();
			var results = await response.Content.ReadFromJsonAsync<List<TaskDto>>();
			Assert.NotNull(results);
			Assert.Single(results);
			Assert.Equal("Bravo", results[0].Title);
		}

		[Fact]
		public async System.Threading.Tasks.Task DeleteTask_ArchivesAndExcludesFromListings()
		{
			var keep = await CreateTaskAsync(new TaskCreateDto { Title = "Keep", Priority = "low" });
			var archive = await CreateTaskAsync(new TaskCreateDto { Title = "Archive", Priority = "medium" });

			var deleteResponse = await _client.DeleteAsync($"/api/tasks/{archive.Uid}");
			deleteResponse.EnsureSuccessStatusCode();

			var listResponse = await _client.GetAsync("/api/tasks");
			listResponse.EnsureSuccessStatusCode();
			var tasks = await listResponse.Content.ReadFromJsonAsync<List<TaskDto>>();
			Assert.NotNull(tasks);
			Assert.Single(tasks);
			Assert.Equal(keep.Uid, tasks[0].Uid);
		}

		[Fact]
		public async System.Threading.Tasks.Task ImportTasks_JsonDefaultsPriorityAndStatus()
		{
			const string defaultStatus = "todo";
			var payload = new List<TaskImportDto>
			{
				new TaskImportDto { Title = "Imported", Description = "Defaults" },
				new TaskImportDto { Title = "" }
			};

			var response = await _client.PostAsJsonAsync("/api/tasks/import?format=json", payload);
			response.EnsureSuccessStatusCode();
			var result = await response.Content.ReadFromJsonAsync<ImportResponse>();
			Assert.NotNull(result);
			Assert.Equal(1, result.Imported);
			Assert.Equal(2, result.Total);
			Assert.NotNull(result.Errors);
			Assert.Single(result.Tasks);
			Assert.Equal("medium", result.Tasks[0].Priority);
			Assert.Equal(defaultStatus, result.Tasks[0].Status);
		}

		[Fact]
		public async System.Threading.Tasks.Task ImportTasks_CsvImportsAndRespectsArchivedFlag()
		{
			var csv = string.Join("\n", new[]
			{
				"Title,Description,Priority,DueDate,Tags,Status,Archived",
				"Keep,Keep task,low,2024-02-01,\"work,urgent\",todo,0",
				"Archive,Archive task,high,2024-02-02,old,todo,1"
			});

			var content = new StringContent(JsonSerializer.Serialize(csv), Encoding.UTF8, "application/json");
			var response = await _client.PostAsync("/api/tasks/import?format=csv", content);
			response.EnsureSuccessStatusCode();

			var listResponse = await _client.GetAsync("/api/tasks");
			listResponse.EnsureSuccessStatusCode();
			var tasks = await listResponse.Content.ReadFromJsonAsync<List<TaskDto>>();
			Assert.NotNull(tasks);
			Assert.Single(tasks);
			Assert.Equal("Keep", tasks[0].Title);
		}

		[Fact]
		public async System.Threading.Tasks.Task GetTags_ReturnsUniqueSortedTagsExcludingArchived()
		{
			var keep = await CreateTaskAsync(new TaskCreateDto
			{
				Title = "Tagged",
				Priority = "medium",
				Tags = new List<string> { "beta", "alpha" }
			});
			var archive = await CreateTaskAsync(new TaskCreateDto
			{
				Title = "Archive Tags",
				Priority = "medium",
				Tags = new List<string> { "old" }
			});

			await _client.DeleteAsync($"/api/tasks/{archive.Uid}");

			var response = await _client.GetAsync("/api/tags");
			response.EnsureSuccessStatusCode();
			var tags = await response.Content.ReadFromJsonAsync<List<string>>();
			Assert.NotNull(tags);
			Assert.Equal(new[] { "alpha", "beta" }, tags);
		}

		[Fact]
		public async System.Threading.Tasks.Task GetAssignees_ReturnsDistinctSortedAssignees()
		{
			await CreateTaskAsync(new TaskCreateDto { Title = "A", Priority = "low", Assignee = "zoe" });
			await CreateTaskAsync(new TaskCreateDto { Title = "B", Priority = "low", Assignee = "amy" });
			await CreateTaskAsync(new TaskCreateDto { Title = "C", Priority = "low", Assignee = "amy" });

			var response = await _client.GetAsync("/api/assignees");
			response.EnsureSuccessStatusCode();
			var assignees = await response.Content.ReadFromJsonAsync<List<string>>();
			Assert.NotNull(assignees);
			Assert.Equal(new[] { "amy", "zoe" }, assignees);
		}

		private async System.Threading.Tasks.Task<TaskDto> CreateTaskAsync(TaskCreateDto dto)
		{
			var response = await _client.PostAsJsonAsync("/api/tasks", dto);
			response.EnsureSuccessStatusCode();
			var task = await response.Content.ReadFromJsonAsync<TaskDto>();
			Assert.NotNull(task);
			return task!;
		}

		private sealed class ImportResponse
		{
			[JsonPropertyName("imported")]
			public int Imported { get; set; }

			[JsonPropertyName("total")]
			public int Total { get; set; }

			[JsonPropertyName("tasks")]
			public List<TaskDto> Tasks { get; set; } = new();

			[JsonPropertyName("errors")]
			public List<string>? Errors { get; set; }
		}

		[Fact]
		public async SystemTask GetTasks_DoesNotTriggerNotification_WhenFilteredListEmptyButTodoExists()
		{
			_factory.TelegramProvider.Messages.Clear();

			await _client.PostAsJsonAsync("/api/tasks", new TaskCreateDto
			{
				Title = "Todo Task",
				Priority = "medium",
				Status = "todo"
			});

			var response = await _client.GetAsync("/api/tasks?status=done");
			response.EnsureSuccessStatusCode();

			Assert.Empty(_factory.TelegramProvider.Messages);
		}
	}

	public class TestWebApplicationFactory : WebApplicationFactory<Task.Api.Program>
	{
		private readonly string _testDbPath;
		private Database? _database;

		public TestWebApplicationFactory()
		{
			_testDbPath = Path.Combine(Path.GetTempPath(), $"test_tasks_{Guid.NewGuid()}.db");
			TelegramProvider = new FakeTelegramProvider();
		}

		public string TestDbPath => _testDbPath;

		public FakeTelegramProvider TelegramProvider { get; }

		public void ClearDatabase()
		{
			if (_database != null)
			{
				_database.ClearAllTasksAsync().GetAwaiter().GetResult();
			}
		}

		protected override void ConfigureWebHost(IWebHostBuilder builder)
		{
			builder.UseEnvironment("Testing");
			_database = new Database(_testDbPath);
			builder.ConfigureServices(services =>
			{
				services.AddSingleton<Database>(_database);
				services.AddSingleton<ITelegramProvider>(TelegramProvider);
				services.AddSingleton<TelegramNotificationService>(sp =>
				{
					var options = Options.Create(new TelegramProviderOptions
					{
						Enabled = true,
						BotToken = "token",
						ChatId = "chat",
						DefaultMessage = "No tasks are currently in todo or in_progress."
					});
					return new TelegramNotificationService(
						TelegramProvider,
						options,
						NullLogger<TelegramNotificationService>.Instance);
				});
			});
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

	public sealed class FakeTelegramProvider : ITelegramProvider
	{
		public List<string> Messages { get; } = new();

		public SystemTask SendMessageAsync(string message, System.Threading.CancellationToken cancellationToken = default)
		{
			Messages.Add(message);
			return SystemTask.CompletedTask;
		}
	}
}
