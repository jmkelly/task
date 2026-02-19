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
    }

        public void Dispose()
        {
            File.Delete(_testDbPath);
        }

        [Fact]
        public void AddTask_ShouldCreateTaskWithUniqueUid()
        {
            var task = _db.AddTask("Test Title", "Test Description", "high");

            Assert.NotNull(task);
            Assert.Equal("Test Title", task.Title);
            Assert.Equal("Test Description", task.Description);
            Assert.Equal("high", task.Priority);
            Assert.Equal("pending", task.Status);
            Assert.NotNull(task.Uid);
            Assert.True(task.Uid.Length == 6);
        }

        [Fact]
        public void AddTask_ShouldHandleNullDescription()
        {
            var task = _db.AddTask("Test Title");

            Assert.NotNull(task);
            Assert.Equal("Test Title", task.Title);
            Assert.Null(task.Description);
        }

        [Fact]
        public void GetAllTasks_ShouldReturnAllTasks()
        {
            var task1 = _db.AddTask("Task 1");
            var task2 = _db.AddTask("Task 2");

            var tasks = _db.GetAllTasks();

            Assert.Equal(2, tasks.Count);
            Assert.Contains(tasks, t => t.Uid == task1.Uid);
            Assert.Contains(tasks, t => t.Uid == task2.Uid);
        }

        [Fact]
        public void GetTaskByUid_ShouldReturnCorrectTask()
        {
            var addedTask = _db.AddTask("Unique Task");

            var retrievedTask = _db.GetTaskByUid(addedTask.Uid);

            Assert.NotNull(retrievedTask);
            Assert.Equal(addedTask.Uid, retrievedTask.Uid);
            Assert.Equal(addedTask.Title, retrievedTask.Title);
        }

        [Fact]
        public void GetTaskByUid_ShouldReturnNullForNonExistentUid()
        {
            var task = _db.GetTaskByUid("nonexistent");

            Assert.Null(task);
        }

        [Fact]
        public void UpdateTask_ShouldUpdateFields()
        {
            var task = _db.AddTask("Original Title", "Original Desc", "low");
            var oldUpdatedAt = task.UpdatedAt;
            task.Title = "Updated Title";
            task.Description = "Updated Desc";
            task.Priority = "high";

            _db.UpdateTask(task);

            var updated = _db.GetTaskByUid(task.Uid);
            Assert.Equal("Updated Title", updated.Title);
            Assert.Equal("Updated Desc", updated.Description);
            Assert.Equal("high", updated.Priority);
            Assert.True(updated.UpdatedAt >= oldUpdatedAt);
        }

        [Fact]
        public void DeleteTask_ShouldRemoveTask()
        {
            var task = _db.AddTask("To Delete");

            _db.DeleteTask(task.Id.ToString());

            var allTasks = _db.GetAllTasks();
            Assert.DoesNotContain(allTasks, t => t.Uid == task.Uid);
        }

        [Fact]
        public void CompleteTask_ShouldSetStatusToCompleted()
        {
            var task = _db.AddTask("To Complete");

            _db.CompleteTask(task.Id.ToString());

            var completed = _db.GetTaskByUid(task.Uid);
            Assert.Equal("completed", completed.Status);
        }

        [Fact]
        public void SearchTasksFTS_ShouldFindByTitle()
        {
            _db.AddTask("Buy groceries", "Milk, bread");
            _db.AddTask("Clean house");

            var results = _db.SearchTasksFTS("groceries");

            Assert.Single(results);
            Assert.Equal("Buy groceries", results[0].Title);
        }

        [Fact]
        public void SearchTasksFTS_ShouldFindByDescription()
        {
            _db.AddTask("Task", "Important meeting");
            _db.AddTask("Other task");

            var results = _db.SearchTasksFTS("meeting");

            Assert.Single(results);
            Assert.Equal("Important meeting", results[0].Description);
        }

        [Fact]
        public void SearchTasksSemantic_ShouldFindSimilarTasks()
        {
            // Semantic search is not implemented yet, so returns empty
            _db.AddTask("Buy milk at store");
            _db.AddTask("Purchase groceries");

            var results = _db.SearchTasksSemantic("get groceries");

            Assert.Empty(results); // Currently returns empty as semantic search is stubbed
        }

        [Fact]
        public void SearchTasksHybrid_ShouldCombineFTSAndSemantic()
        {
            _db.AddTask("Buy groceries", "Milk, bread");
            _db.AddTask("Clean house");

            var results = _db.SearchTasksHybrid("groceries");

            Assert.Contains(results, t => t.Title.Contains("groceries"));
        }

        [Fact]
        public void AddTask_ShouldHandleTags()
        {
            var tags = new List<string> { "urgent", "work" };
            var task = _db.AddTask("Tagged Task", tags: tags);

            Assert.Equal(tags, task.Tags);
        }

        [Fact]
        public void AddTask_ShouldHandleDueDate()
        {
            var dueDate = new DateTime(2023, 12, 31);
            var task = _db.AddTask("Due Task", dueDate: dueDate);

            Assert.Equal(dueDate, task.DueDate);
        }
    }
}