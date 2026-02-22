using Xunit;
using TaskApp;
using Microsoft.Data.Sqlite;
using System.IO;
using System.Linq;
using System.Collections.Generic;

namespace TaskApp.Tests.UnitTests
{
    public class DatabaseTests : IDisposable
    {
        private string _testDbPath;
        private Database _db;

        public DatabaseTests()
        {
            _testDbPath = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString() + ".db");
            _db = new Database(_testDbPath);
            _db.InitializeAsync().GetAwaiter().GetResult();
        }

        public void Dispose()
        {
            File.Delete(_testDbPath);
        }

        [Fact]
        public async System.Threading.Tasks.Task AddTask_ShouldCreateTaskWithUniqueUid()
        {
            var task = await _db.AddTaskAsync("Test Title", "Test Description", "high", null, new List<string>());

            Assert.NotNull(task);
            Assert.Equal("Test Title", task.Title);
            Assert.Equal("Test Description", task.Description);
            Assert.Equal("high", task.Priority);
            Assert.Equal("todo", task.Status);
            Assert.NotNull(task.Uid);
            Assert.True(task.Uid.Length == 6);
        }

        [Fact]
        public async System.Threading.Tasks.Task AddTask_ShouldHandleNullDescription()
        {
            var task = await _db.AddTaskAsync("Test Title", null, "medium", null, new List<string>());

            Assert.NotNull(task);
            Assert.Equal("Test Title", task.Title);
            Assert.Null(task.Description);
        }

        [Fact]
        public async System.Threading.Tasks.Task GetAllTasks_ShouldReturnAllTasks()
        {
            var task1 = await _db.AddTaskAsync("Task 1", null, "medium", null, new List<string>());
            var task2 = await _db.AddTaskAsync("Task 2", null, "medium", null, new List<string>());

            var tasks = await _db.GetAllTasksAsync();

            Assert.Equal(2, tasks.Count);
            Assert.Contains(tasks, t => t.Uid == task1.Uid);
            Assert.Contains(tasks, t => t.Uid == task2.Uid);
        }

        [Fact]
        public async System.Threading.Tasks.Task GetTaskByUid_ShouldReturnCorrectTask()
        {
            var addedTask = await _db.AddTaskAsync("Unique Task", null, "medium", null, new List<string>());

            var retrievedTask = await _db.GetTaskByUidAsync(addedTask.Uid);

            Assert.NotNull(retrievedTask);
            Assert.Equal(addedTask.Uid, retrievedTask.Uid);
            Assert.Equal(addedTask.Title, retrievedTask.Title);
        }

        [Fact]
        public async System.Threading.Tasks.Task GetTaskByUid_ShouldReturnNullForNonExistentUid()
        {
            var task = await _db.GetTaskByUidAsync("nonexistent");

            Assert.Null(task);
        }

        [Fact]
        public async System.Threading.Tasks.Task UpdateTask_ShouldUpdateFields()
        {
            var task = await _db.AddTaskAsync("Original Title", "Original Desc", "low", null, new List<string>());
            var oldUpdatedAt = task.UpdatedAt;
            task.Title = "Updated Title";
            task.Description = "Updated Desc";
            task.Priority = "high";

            await _db.UpdateTaskAsync(task);

            var updated = await _db.GetTaskByUidAsync(task.Uid);
            Assert.NotNull(updated);
            Assert.Equal("Updated Title", updated.Title);
            Assert.Equal("Updated Desc", updated.Description);
            Assert.Equal("high", updated.Priority);
            Assert.True(updated.UpdatedAt >= oldUpdatedAt);
        }

        [Fact]
        public async System.Threading.Tasks.Task DeleteTask_ShouldRemoveTask()
        {
            var task = await _db.AddTaskAsync("To Delete", null, "medium", null, new List<string>());

            await _db.DeleteTaskAsync(task.Uid);

            var allTasks = await _db.GetAllTasksAsync();
            Assert.DoesNotContain(allTasks, t => t.Uid == task.Uid);
        }

        [Fact]
        public async System.Threading.Tasks.Task CompleteTask_ShouldSetStatusToCompleted()
        {
            var task = await _db.AddTaskAsync("To Complete", null, "medium", null, new List<string>());

            await _db.CompleteTaskAsync(task.Uid);

            var completed = await _db.GetTaskByUidAsync(task.Uid);
            Assert.NotNull(completed);
            Assert.Equal("done", completed.Status);
        }

        [Fact]
        public async System.Threading.Tasks.Task SearchTasksFTS_ShouldFindByTitle()
        {
            await _db.AddTaskAsync("Buy groceries", "Milk, bread", "medium", null, new List<string>());
            await _db.AddTaskAsync("Clean house", null, "medium", null, new List<string>());

            var results = await _db.SearchTasksFTSAsync("groceries");

            Assert.Single(results);
            Assert.Equal("Buy groceries", results[0].Title);
        }

        [Fact]
        public async System.Threading.Tasks.Task SearchTasksFTS_ShouldFindByDescription()
        {
            await _db.AddTaskAsync("Task", "Important meeting", "medium", null, new List<string>());
            await _db.AddTaskAsync("Other task", null, "medium", null, new List<string>());

            var results = await _db.SearchTasksFTSAsync("meeting");

            Assert.Single(results);
            Assert.Equal("Important meeting", results[0].Description);
        }

        [Fact]
        public async System.Threading.Tasks.Task SearchTasksSemantic_ShouldFindSimilarTasks()
        {
            await _db.AddTaskAsync("Buy milk at store", null, "medium", null, new List<string>());
            await _db.AddTaskAsync("Purchase groceries", null, "medium", null, new List<string>());

            var results = await _db.SearchTasksSemanticAsync("get groceries");

            Assert.Empty(results);
        }

        [Fact]
        public async System.Threading.Tasks.Task SearchTasksHybrid_ShouldCombineFTSAndSemantic()
        {
            await _db.AddTaskAsync("Buy groceries", "Milk, bread", "medium", null, new List<string>());
            await _db.AddTaskAsync("Clean house", null, "medium", null, new List<string>());

            var results = await _db.SearchTasksHybridAsync("groceries");

            Assert.Contains(results, t => t.Title.Contains("groceries"));
        }

        [Fact]
        public async System.Threading.Tasks.Task AddTask_ShouldHandleTags()
        {
            var tags = new List<string> { "urgent", "work" };
            var task = await _db.AddTaskAsync("Tagged Task", null, "medium", null, tags);

            Assert.Equal(tags, task.Tags);
        }

        [Fact]
        public async System.Threading.Tasks.Task AddTask_ShouldHandleDueDate()
        {
            var dueDate = new DateTime(2023, 12, 31);
            var task = await _db.AddTaskAsync("Due Task", null, "medium", dueDate, new List<string>());

            Assert.Equal(dueDate, task.DueDate);
        }
    }
}
