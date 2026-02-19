using Xunit;
using TaskApp;
using Spectre.Console.Testing;
using Spectre.Console.Cli;
using System.IO;
using System.Threading;
using System;

namespace TaskApp.Tests.IntegrationTests
{
    internal class MockRemainingArguments : IRemainingArguments
    {
        public IReadOnlyList<string> Raw => new List<string>();
        public ILookup<string, string?> Parsed => new List<string>().ToLookup(s => s, s => (string?)null);
    }

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
        public void EndToEnd_AddAndListTasks()
        {
            var dbPath = $"/tmp/test_task_{Guid.NewGuid()}.db";
            try
            {
                // Test the full workflow through database operations
                var db = new Database(dbPath);
                
                // Add a task
                var task = db.AddTask("Integration Test Task", "Test description", "high");
                
                // List all tasks
                var tasks = db.GetAllTasks();
                
                Assert.Single(tasks);
                Assert.Equal("Integration Test Task", tasks[0].Title);
                Assert.Equal("Test description", tasks[0].Description);
                Assert.Equal("high", tasks[0].Priority);
                Assert.Equal("pending", tasks[0].Status);
            }
            finally
            {
                File.Delete(dbPath);
            }
        }

        [Fact]
        public void EndToEnd_AddEditCompleteWorkflow()
        {
            var dbPath = $"/tmp/test_task_{Guid.NewGuid()}.db";
            try
            {
                var db = new Database(dbPath);
                
                // Add task
                var task = db.AddTask("Workflow Task", "Original description", "medium");
                
                // Edit task
                task.Title = "Updated Workflow Task";
                task.Description = "Updated description";
                task.Priority = "high";
                db.UpdateTask(task);
                
                // Complete task
                db.CompleteTask(task.Id.ToString());
                
                // Verify final state
                var finalTask = db.GetTaskByUid(task.Uid);
                Assert.NotNull(finalTask);
                Assert.Equal("Updated Workflow Task", finalTask.Title);
                Assert.Equal("Updated description", finalTask.Description);
                Assert.Equal("high", finalTask.Priority);
                Assert.Equal("completed", finalTask.Status);
            }
            finally
            {
                File.Delete(dbPath);
            }
        }

        [Fact]
        public void EndToEnd_FullTextSearch()
        {
            var dbPath = $"/tmp/test_task_{Guid.NewGuid()}.db";
            try
            {
                var db = new Database(dbPath);
                
                // Add tasks
                db.AddTask("Buy groceries", "Milk, bread, eggs");
                db.AddTask("Clean the house", "Vacuum and dust");
                db.AddTask("Write report", "Q4 financial report");
                
                // Search for "house"
                var results = db.SearchTasksFTS("house");
                
                Assert.Single(results);
                Assert.Equal("Clean the house", results[0].Title);
            }
            finally
            {
                File.Delete(dbPath);
            }
        }

        [Fact]
        public void EndToEnd_HybridSearch()
        {
            var dbPath = $"/tmp/test_task_{Guid.NewGuid()}.db";
            try
            {
                var db = new Database(dbPath);
                
                // Add tasks
                db.AddTask("Buy groceries", "Milk and bread");
                db.AddTask("Purchase items", "Shopping list");
                
                // Hybrid search (currently just FTS since semantic is stubbed)
                var results = db.SearchTasksHybrid("buy");
                
                Assert.Contains(results, t => t.Title.Contains("Buy"));
            }
            finally
            {
                File.Delete(dbPath);
            }
        }
    }
}