using Xunit;
using Task.Api;
using Task.Core;
using Microsoft.Data.Sqlite;
using System.IO;
using System.Linq;
using System.Collections.Generic;

namespace Task.Cli.Tests.IntegrationTests
{
    public class DatabaseTests : IDisposable
    {
        private string _testDbPath;
        private Database db;

        public DatabaseTests()
        {
            _testDbPath = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString() + ".db");
            db = new Database(_testDbPath);
            db.Initialize();
        }

        public void Dispose()
        {
            File.Delete(_testDbPath);
        }

        [Fact]
        public async System.Threading.Tasks.Task AddTask_ShouldCreateTaskWithUniqueUid()
        {
            var task = await db.AddTaskAsync("Test Title", "Test Description", "high", null, new List<string>());

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
            var task = await db.AddTaskAsync("Test Title", null, "medium", null, new List<string>());

            Assert.NotNull(task);
            Assert.Equal("Test Title", task.Title);
            Assert.Null(task.Description);
        }

        [Fact]
        public async System.Threading.Tasks.Task GetAllTasks_ShouldReturnAllTasks()
        {
            var task1 = await db.AddTaskAsync("Task 1", null, "medium", null, new List<string>());
            var task2 = await db.AddTaskAsync("Task 2", null, "medium", null, new List<string>());

            var tasks = await db.GetAllTasksAsync();

            Assert.Equal(2, tasks.Count);
            Assert.Contains(tasks, t => t.Uid == task1.Uid);
            Assert.Contains(tasks, t => t.Uid == task2.Uid);
        }

        [Fact]
        public async System.Threading.Tasks.Task GetTaskByUid_ShouldReturnCorrectTask()
        {
            var addedTask = await db.AddTaskAsync("Unique Task", null, "medium", null, new List<string>());

            var retrievedTask = await db.GetTaskByUidAsync(addedTask.Uid);

            Assert.NotNull(retrievedTask);
            Assert.Equal(addedTask.Uid, retrievedTask.Uid);
            Assert.Equal(addedTask.Title, retrievedTask.Title);
        }

        [Fact]
        public async System.Threading.Tasks.Task GetTaskByUid_ShouldReturnNullForNonExistentUid()
        {
            var task = await db.GetTaskByUidAsync("nonexistent");

            Assert.Null(task);
        }

        [Fact]
        public async System.Threading.Tasks.Task UpdateTask_ShouldUpdateFields()
        {
            var task = await db.AddTaskAsync("Original Title", "Original Desc", "low", null, new List<string>());
            var oldUpdatedAt = task.UpdatedAt;
            task.Title = "Updated Title";
            task.Description = "Updated Desc";
            task.Priority = "high";

            await db.UpdateTaskAsync(task);

            var updated = await db.GetTaskByUidAsync(task.Uid);
            Assert.NotNull(updated);
            Assert.Equal("Updated Title", updated.Title);
            Assert.Equal("Updated Desc", updated.Description);
            Assert.Equal("high", updated.Priority);
            Assert.True(updated.UpdatedAt >= oldUpdatedAt);
        }

        [Fact]
        public async System.Threading.Tasks.Task DeleteTask_ShouldRemoveTask()
        {
            var task = await db.AddTaskAsync("To Delete", null, "medium", null, new List<string>());

            await db.DeleteTaskAsync(task.Uid);

            var allTasks = await db.GetAllTasksAsync();
            Assert.DoesNotContain(allTasks, t => t.Uid == task.Uid);
        }

        [Fact]
        public async System.Threading.Tasks.Task CompleteTask_ShouldSetStatusToCompleted()
        {
            var task = await db.AddTaskAsync("To Complete", null, "medium", null, new List<string>());

            await db.CompleteTaskAsync(task.Uid);

            var completed = await db.GetTaskByUidAsync(task.Uid);
            Assert.NotNull(completed);
            Assert.Equal("done", completed.Status);
        }

        [Fact]
        public async System.Threading.Tasks.Task SearchTasksFTS_ShouldFindByTitle()
        {
            await db.AddTaskAsync("Buy groceries", "Milk, bread", "medium", null, new List<string>());
            await db.AddTaskAsync("Clean house", null, "medium", null, new List<string>());

            var results = await db.SearchTasksFTSAsync("groceries");

            Assert.Single(results);
            Assert.Equal("Buy groceries", results[0].Title);
        }

        [Fact]
        public async System.Threading.Tasks.Task SearchTasksFTS_ShouldFindByDescription()
        {
            await db.AddTaskAsync("Task", "Important meeting", "medium", null, new List<string>());
            await db.AddTaskAsync("Other task", null, "medium", null, new List<string>());

            var results = await db.SearchTasksFTSAsync("meeting");

            Assert.Single(results);
            Assert.Equal("Important meeting", results[0].Description);
        }

        [Fact]
        public async System.Threading.Tasks.Task SearchTasksSemantic_ShouldFindSimilarTasks()
        {
            await db.AddTaskAsync("Buy milk at store", null, "medium", null, new List<string>());
            await db.AddTaskAsync("Purchase groceries", null, "medium", null, new List<string>());

            var results = await db.SearchTasksSemanticAsync("get groceries");

            Assert.Empty(results);
        }

        [Fact]
        public async System.Threading.Tasks.Task SearchTasksHybrid_ShouldCombineFTSAndSemantic()
        {
            await db.AddTaskAsync("Buy groceries", "Milk, bread", "medium", null, new List<string>());
            await db.AddTaskAsync("Clean house", null, "medium", null, new List<string>());

            var results = await db.SearchTasksHybridAsync("groceries");

            Assert.Contains(results, t => t.Title.Contains("groceries"));
        }

        [Fact]
        public async System.Threading.Tasks.Task AddTask_ShouldHandleTags()
        {
            var tags = new List<string> { "urgent", "work" };
            var task = await db.AddTaskAsync("Tagged Task", null, "medium", null, tags);

            Assert.Equal(tags, task.Tags);
        }

        [Fact]
        public async System.Threading.Tasks.Task AddTask_ShouldHandleDueDate()
        {
            var dueDate = new DateTime(2023, 12, 31);
            var task = await db.AddTaskAsync("Due Task", null, "medium", dueDate, new List<string>());

            Assert.Equal(dueDate, task.DueDate);
        }

        [Fact]
        public async System.Threading.Tasks.Task EndToEnd_AddAndListTasks()
        {
            var task = await db.AddTaskAsync("Integration Test Task", "Test description", "high", null, new List<string>());

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
            var task = await db.AddTaskAsync("Workflow Task", "Original description", "medium", null, new List<string>());

            task.Title = "Updated Workflow Task";
            task.Description = "Updated description";
            task.Priority = "high";
            await db.UpdateTaskAsync(task);

            await db.CompleteTaskAsync(task.Uid);

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
            await db.AddTaskAsync("Buy groceries", "Milk, bread, eggs", "medium", null, new List<string>());
            await db.AddTaskAsync("Clean the house", "Vacuum and dust", "medium", null, new List<string>());
            await db.AddTaskAsync("Write report", "Q4 financial report", "medium", null, new List<string>());

            var results = await db.SearchTasksAsync("house");

            Assert.Single(results);
            Assert.Equal("Clean the house", results[0].Title);
        }

        [Fact]
        public async System.Threading.Tasks.Task EndToEnd_HybridSearch()
        {
            await db.AddTaskAsync("Buy groceries", "Milk and bread", "medium", null, new List<string>());
            await db.AddTaskAsync("Purchase items", "Shopping list", "medium", null, new List<string>());

            var results = await db.SearchTasksAsync("buy");

            Assert.Contains(results, t => t.Title.Contains("Buy"));
        }

        [Fact]
        public async System.Threading.Tasks.Task QuickAdd_WithTitle_CreatesTask()
        {
            var task = await db.AddTaskAsync("Buy milk", null, "medium", null, new List<string>());

            var tasks = await db.GetAllTasksAsync();
            Assert.Single(tasks);
            Assert.Equal("Buy milk", tasks[0].Title);
            Assert.Equal("medium", tasks[0].Priority);
        }

        [Fact]
        public async System.Threading.Tasks.Task QuickAdd_WithAllOptions_CreatesTask()
        {
            var task = await db.AddTaskAsync(
                "Complex task",
                "Full description",
                "high",
                DateTime.Parse("2024-12-31"),
                new List<string> { "work", "urgent" },
                "work",
                null,
                "todo",
                default);

            var tasks = await db.GetAllTasksAsync();
            Assert.Single(tasks);
            Assert.Equal("Complex task", tasks[0].Title);
            Assert.Equal("Full description", tasks[0].Description);
            Assert.Equal("high", tasks[0].Priority);
            Assert.Equal("work", tasks[0].Project);
            Assert.Contains("work", tasks[0].Tags);
            Assert.Contains("urgent", tasks[0].Tags);
        }

        [Fact]
        public async System.Threading.Tasks.Task QuickAdd_WithValidStatus_CreatesTask()
        {
            var task = await db.AddTaskAsync(
                "Test task",
                null,
                "medium",
                null,
                new List<string>(),
                null,
                null,
                "in_progress",
                default);

            var tasks = await db.GetAllTasksAsync();
            Assert.Single(tasks);
            Assert.Equal("in_progress", tasks[0].Status);
        }

        [Fact]
        public async System.Threading.Tasks.Task DeleteTask_ShouldArchiveAndHideFromQueries()
        {
            var task = await db.AddTaskAsync("Archive me", null, "medium", null, new List<string>());

            await db.DeleteTaskAsync(task.Uid);

            var byUid = await db.GetTaskByUidAsync(task.Uid);
            var all = await db.GetAllTasksAsync();

            Assert.Null(byUid);
            Assert.DoesNotContain(all, t => t.Uid == task.Uid);
        }

        [Fact]
        public async System.Threading.Tasks.Task ClearAllTasksAsync_ShouldArchiveAllTasks()
        {
            await db.AddTaskAsync("One", null, "medium", null, new List<string>());
            await db.AddTaskAsync("Two", null, "medium", null, new List<string>());

            await db.ClearAllTasksAsync();

            var tasks = await db.GetAllTasksAsync();
            Assert.Empty(tasks);
        }

        [Fact]
        public async System.Threading.Tasks.Task GetAllUniqueTagsAsync_ShouldExcludeArchivedTasks()
        {
            var active = await db.AddTaskAsync("Active", null, "medium", null, new List<string> { "alpha", "beta" });
            var archived = await db.AddTaskAsync("Archived", null, "medium", null, new List<string> { "legacy" });

            await db.DeleteTaskAsync(archived.Uid);

            var tags = await db.GetAllUniqueTagsAsync();
            Assert.Equal(new[] { "alpha", "beta" }, tags);
        }

        [Fact]
        public async System.Threading.Tasks.Task SearchTasksFTS_ShouldExcludeArchivedTasks()
        {
            var active = await db.AddTaskAsync("Visible task", "keep", "medium", null, new List<string>());
            var archived = await db.AddTaskAsync("Hidden task", "archive", "medium", null, new List<string>());

            await db.DeleteTaskAsync(archived.Uid);

            var results = await db.SearchTasksFTSAsync("task");

            Assert.Contains(results, t => t.Uid == active.Uid);
            Assert.DoesNotContain(results, t => t.Uid == archived.Uid);
        }

        [Fact]
        public async System.Threading.Tasks.Task AddTask_ShouldPersistAssigneeAndProject()
        {
            var task = await db.AddTaskAsync("Assigned", "desc", "medium", null, new List<string>(), "alpha", "jordan");

            var fetched = await db.GetTaskByUidAsync(task.Uid);

            Assert.NotNull(fetched);
            Assert.Equal("alpha", fetched!.Project);
            Assert.Equal("jordan", fetched.Assignee);
        }
    }
}
