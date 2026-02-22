using Xunit;
using TaskApp;
using System.IO;
using System.Threading;
using System;
using System.Threading.Tasks;

namespace TaskApp.Tests.IntegrationTests
{
    public class CliIntegrationTests : IDisposable
    {
        private readonly string _testDbPath;

        public CliIntegrationTests()
        {
            _testDbPath = $"/tmp/test_task_{Guid.NewGuid()}.db";
        }

        public void Dispose()
        {
            if (File.Exists(_testDbPath))
            {
                File.Delete(_testDbPath);
            }
        }

        [Fact]
        public async System.Threading.Tasks.Task EndToEnd_AddAndListTasks()
        {
            ITaskService db = new Database(_testDbPath);
            await db.InitializeAsync();

            var task = await db.AddTaskAsync("Integration Test Task", "Test description", "high", null, new List<string>());

            // List all tasks
            var tasks = await db.GetAllTasksAsync();

            Assert.Single(tasks);
            Assert.Equal("Integration Test Task", tasks[0].Title);
            Assert.Equal("Test description", tasks[0].Description);
            Assert.Equal("high", tasks[0].Priority);
            Assert.Equal("todo", tasks[0].Status);
        }

        [Fact]
        public async System.Threading.Tasks.Task EndToEnd_AddEditCompleteWorkflow()
        {
            ITaskService db = new Database(_testDbPath);
            await db.InitializeAsync();

            var task = await db.AddTaskAsync("Workflow Task", "Original description", "medium", null, new List<string>());

            // Edit task
            task.Title = "Updated Workflow Task";
            task.Description = "Updated description";
            task.Priority = "high";
            await db.UpdateTaskAsync(task);

            // Complete task
            await db.CompleteTaskAsync(task.Uid);

            // Verify final state
            var finalTask = await db.GetTaskByUidAsync(task.Uid);
            Assert.NotNull(finalTask);
            Assert.Equal("Updated Workflow Task", finalTask.Title);
            Assert.Equal("Updated description", finalTask.Description);
            Assert.Equal("high", finalTask.Priority);
            Assert.Equal("done", finalTask.Status);
        }

        [Fact]
        public async System.Threading.Tasks.Task EndToEnd_FullTextSearch()
        {
            ITaskService db = new Database(_testDbPath);
            await db.InitializeAsync();

            await db.AddTaskAsync("Buy groceries", "Milk, bread, eggs", "medium", null, new List<string>());
            await db.AddTaskAsync("Clean the house", "Vacuum and dust", "medium", null, new List<string>());
            await db.AddTaskAsync("Write report", "Q4 financial report", "medium", null, new List<string>());

            // Search for "house"
            var results = await db.SearchTasksAsync("house");

            Assert.Single(results);
            Assert.Equal("Clean the house", results[0].Title);
        }

        [Fact]
        public async System.Threading.Tasks.Task EndToEnd_HybridSearch()
        {
            ITaskService db = new Database(_testDbPath);
            await db.InitializeAsync();

            await db.AddTaskAsync("Buy groceries", "Milk and bread", "medium", null, new List<string>());
            await db.AddTaskAsync("Purchase items", "Shopping list", "medium", null, new List<string>());

            // Hybrid search (currently just FTS since semantic is stubbed)
            var results = await db.SearchTasksAsync("buy");

            Assert.Contains(results, t => t.Title.Contains("Buy"));
        }
    }
}