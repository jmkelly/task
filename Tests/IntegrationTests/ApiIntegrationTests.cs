using Xunit;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.DependencyInjection;
using System.Net.Http;
using System.Net.Http.Json;
using System.Threading.Tasks;
using System.Collections.Generic;
using System.IO;
using Task.Core;

namespace Task.Cli.Tests.IntegrationTests
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
        public async System.Threading.Tasks.Task GetTasks_ReturnsEmptyList_WhenNoTasks()
        {
            var response = await _client.GetAsync("/api/tasks");
            response.EnsureSuccessStatusCode();
            var tasks = await response.Content.ReadFromJsonAsync<List<TaskDto>>();
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

            Assert.Equal("Test Task", task.Title);
            Assert.Equal("Test Description", task.Description);
            Assert.Equal("high", task.Priority);
            Assert.Equal("todo", task.Status);
        }

        [Fact]
        public async System.Threading.Tasks.Task GetTask_ReturnsTask_WhenExists()
        {
            var newTask = new TaskCreateDto { Title = "Get Test", Priority = "medium" };
            var createResponse = await _client.PostAsJsonAsync("/api/tasks", newTask);
            var createdTask = await createResponse.Content.ReadFromJsonAsync<TaskDto>();

            // Then get it
            var getResponse = await _client.GetAsync($"/api/tasks/{createdTask.Uid}");
            getResponse.EnsureSuccessStatusCode();
            var retrievedTask = await getResponse.Content.ReadFromJsonAsync<TaskDto>();

            Assert.Equal(createdTask.Uid, retrievedTask.Uid);
            Assert.Equal("Get Test", retrievedTask.Title);
        }

        [Fact]
        public async System.Threading.Tasks.Task UpdateTask_UpdatesExistingTask()
        {
            var newTask = new TaskCreateDto { Title = "Original", Priority = "low" };
            var createResponse = await _client.PostAsJsonAsync("/api/tasks", newTask);
            var createdTask = await createResponse.Content.ReadFromJsonAsync<TaskDto>();

            // Update it
            var updateDto = new TaskUpdateDto
            {
                Title = "Updated",
                Priority = "high"
            };
            var updateResponse = await _client.PutAsJsonAsync($"/api/tasks/{createdTask.Uid}", updateDto);
            updateResponse.EnsureSuccessStatusCode();

            // Verify update
            var getResponse = await _client.GetAsync($"/api/tasks/{createdTask.Uid}");
            var updatedTask = await getResponse.Content.ReadFromJsonAsync<TaskDto>();
            Assert.Equal("Updated", updatedTask.Title);
            Assert.Equal("high", updatedTask.Priority);
        }

        [Fact]
        public async System.Threading.Tasks.Task DeleteTask_RemovesTask()
        {
            var newTask = new TaskCreateDto { Title = "To Delete", Priority = "medium" };
            var createResponse = await _client.PostAsJsonAsync("/api/tasks", newTask);
            var createdTask = await createResponse.Content.ReadFromJsonAsync<TaskDto>();

            // Delete it
            var deleteResponse = await _client.DeleteAsync($"/api/tasks/{createdTask.Uid}");
            deleteResponse.EnsureSuccessStatusCode();

            // Verify deletion
            var getResponse = await _client.GetAsync($"/api/tasks/{createdTask.Uid}");
            Assert.Equal(System.Net.HttpStatusCode.NotFound, getResponse.StatusCode);
        }

        [Fact]
        public async System.Threading.Tasks.Task CompleteTask_SetsStatusToCompleted()
        {
            var newTask = new TaskCreateDto { Title = "To Complete", Priority = "medium" };
            var createResponse = await _client.PostAsJsonAsync("/api/tasks", newTask);
            var createdTask = await createResponse.Content.ReadFromJsonAsync<TaskDto>();

            // Complete it
            var completeResponse = await _client.PatchAsync($"/api/tasks/{createdTask.Uid}/complete", null);
            completeResponse.EnsureSuccessStatusCode();

            // Verify completion
            var getResponse = await _client.GetAsync($"/api/tasks/{createdTask.Uid}");
            var completedTask = await getResponse.Content.ReadFromJsonAsync<TaskDto>();
            Assert.Equal("done", completedTask.Status);
        }

        [Fact]
        public async System.Threading.Tasks.Task SearchTasks_ReturnsMatchingTasks()
        {
            await _client.PostAsJsonAsync("/api/tasks", new TaskCreateDto { Title = "Buy groceries", Priority = "medium" });
            await _client.PostAsJsonAsync("/api/tasks", new TaskCreateDto { Title = "Clean house", Priority = "medium" });

            // Search
            var searchResponse = await _client.GetAsync("/api/tasks/search?q=groceries");
            searchResponse.EnsureSuccessStatusCode();
            var results = await searchResponse.Content.ReadFromJsonAsync<List<TaskDto>>();

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
}