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
            Assert.Equal(System.Net.HttpStatusCode.NotFound, getResponse.StatusCode);
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
            Assert.Equal(System.Net.HttpStatusCode.NotFound, response.StatusCode);
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
