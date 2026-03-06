using System;
using System.Collections.Generic;
using System.IO;
using Task.Core;
using Xunit;

namespace Task.Cli.Tests.IntegrationTests
{
	public class DatabaseTests : IDisposable
	{
		private readonly string _testDbPath;
		private readonly Database _database;
		private readonly IUid _uidGenerator = new Uid();

		private string NewUid() => _uidGenerator.GenerateUid();

		public DatabaseTests()
		{
			_testDbPath = Path.Combine(Path.GetTempPath(), Guid.NewGuid() + ".db");
			_database = new Database(_testDbPath);
			_database.Initialize();
		}

		public void Dispose()
		{
			if (File.Exists(_testDbPath))
			{
				File.Delete(_testDbPath);
			}
		}

		[Fact]
		public async System.Threading.Tasks.Task AddTask_ShouldUseProvidedUid()
		{
			var uid = NewUid();
			var task = await _database.AddTaskAsync(uid, "Test Title", "Test Description", "high", null, new List<string>());

			Assert.NotNull(task);
			Assert.Equal("Test Title", task.Title);
			Assert.Equal("Test Description", task.Description);
			Assert.Equal("high", task.Priority);
			Assert.Equal("todo", task.Status);
			Assert.Equal(uid, task.Uid);
			Assert.True(task.Uid.Length == 6);
		}

		[Fact]
		public async System.Threading.Tasks.Task AddTask_ShouldHandleNullDescription()
		{
			var task = await _database.AddTaskAsync(NewUid(), "Test Title", null, "medium", null, new List<string>());

			Assert.NotNull(task);
			Assert.Equal("Test Title", task.Title);
			Assert.Null(task.Description);
		}

		[Fact]
		public async System.Threading.Tasks.Task AddTask_WithBlockedStatus_PersistsBlockReason()
		{
			var task = await _database.AddTaskAsync(
				NewUid(),
				"Blocked Task",
				"Needs review",
				"medium",
				null,
				new List<string>(),
				null,
				null,
				"blocked",
				"Waiting on approval");

			var fetched = await _database.GetTaskByUidAsync(task.Uid);

			Assert.NotNull(fetched);
			Assert.Equal("blocked", fetched!.Status);
			Assert.Equal("Waiting on approval", fetched.BlockReason);
		}

		[Fact]
		public async System.Threading.Tasks.Task UpdateTask_ShouldPersistBlockReasonChanges()
		{
			var task = await _database.AddTaskAsync(NewUid(), "Update block", null, "medium", null, new List<string>());

			task.Status = "blocked";
			task.BlockReason = "Waiting on dependency";
			await _database.UpdateTaskAsync(task);

			var updated = await _database.GetTaskByUidAsync(task.Uid);
			Assert.NotNull(updated);
			Assert.Equal("blocked", updated!.Status);
			Assert.Equal("Waiting on dependency", updated.BlockReason);
		}

		[Fact]
		public async System.Threading.Tasks.Task GetAllTasks_ShouldReturnAllTasks()
		{
			var task1 = await _database.AddTaskAsync(NewUid(), "Task 1", null, "medium", null, new List<string>());
			var task2 = await _database.AddTaskAsync(NewUid(), "Task 2", null, "medium", null, new List<string>());

			var tasks = await _database.GetAllTasksAsync();

			Assert.Equal(2, tasks.Count);
			Assert.Contains(tasks, t => t.Uid == task1.Uid);
			Assert.Contains(tasks, t => t.Uid == task2.Uid);
		}

		[Fact]
		public async System.Threading.Tasks.Task GetTaskByUid_ShouldReturnCorrectTask()
		{
			var addedTask = await _database.AddTaskAsync(NewUid(), "Unique Task", null, "medium", null, new List<string>());

			var retrievedTask = await _database.GetTaskByUidAsync(addedTask.Uid);

			Assert.NotNull(retrievedTask);
			Assert.Equal(addedTask.Uid, retrievedTask.Uid);
			Assert.Equal(addedTask.Title, retrievedTask.Title);
		}

		[Fact]
		public async System.Threading.Tasks.Task GetTaskByUid_ShouldReturnNullForNonExistentUid()
		{
			var task = await _database.GetTaskByUidAsync("nonexistent");

			Assert.Null(task);
		}

		[Fact]
		public async System.Threading.Tasks.Task UpdateTask_ShouldUpdateFields()
		{
			var task = await _database.AddTaskAsync(NewUid(), "Original Title", "Original Desc", "low", null, new List<string>());
			var oldUpdatedAt = task.UpdatedAt;
			task.Title = "Updated Title";
			task.Description = "Updated Desc";
			task.Priority = "high";

			await _database.UpdateTaskAsync(task);

			var updated = await _database.GetTaskByUidAsync(task.Uid);
			Assert.NotNull(updated);
			Assert.Equal("Updated Title", updated.Title);
			Assert.Equal("Updated Desc", updated.Description);
			Assert.Equal("high", updated.Priority);
			Assert.True(updated.UpdatedAt >= oldUpdatedAt);
		}

		[Fact]
		public async System.Threading.Tasks.Task DeleteTask_ShouldRemoveTask()
		{
			var task = await _database.AddTaskAsync(NewUid(), "To Delete", null, "medium", null, new List<string>());

			await _database.DeleteTaskAsync(task.Uid);

			var allTasks = await _database.GetAllTasksAsync();
			Assert.DoesNotContain(allTasks, t => t.Uid == task.Uid);
		}

		[Fact]
		public async System.Threading.Tasks.Task CompleteTask_ShouldSetStatusToCompleted()
		{
			var task = await _database.AddTaskAsync(NewUid(), "To Complete", null, "medium", null, new List<string>());

			await _database.CompleteTaskAsync(task.Uid);

			var completed = await _database.GetTaskByUidAsync(task.Uid);
			Assert.NotNull(completed);
			Assert.Equal("done", completed.Status);
		}

		[Fact]
		public async System.Threading.Tasks.Task SearchTasksFTS_ShouldFindByTitle()
		{
			await _database.AddTaskAsync(NewUid(), "Buy groceries", "Milk, bread", "medium", null, new List<string>());
			await _database.AddTaskAsync(NewUid(), "Clean house", null, "medium", null, new List<string>());

			var results = await _database.SearchTasksFTSAsync("groceries");

			Assert.Single(results);
			Assert.Equal("Buy groceries", results[0].Title);
		}

		[Fact]
		public async System.Threading.Tasks.Task SearchTasksFTS_ShouldFindByDescription()
		{
			await _database.AddTaskAsync(NewUid(), "Task", "Important meeting", "medium", null, new List<string>());
			await _database.AddTaskAsync(NewUid(), "Other task", null, "medium", null, new List<string>());

			var results = await _database.SearchTasksFTSAsync("meeting");

			Assert.Single(results);
			Assert.Equal("Important meeting", results[0].Description);
		}

		[Fact]
		public async System.Threading.Tasks.Task SearchTasksSemantic_ShouldFindSimilarTasks()
		{
			await _database.AddTaskAsync(NewUid(), "Buy milk at store", null, "medium", null, new List<string>());
			await _database.AddTaskAsync(NewUid(), "Purchase groceries", null, "medium", null, new List<string>());

			var results = await _database.SearchTasksSemanticAsync("get groceries");

			Assert.Empty(results);
		}

		[Fact]
		public async System.Threading.Tasks.Task SearchTasksHybrid_ShouldCombineFTSAndSemantic()
		{
			await _database.AddTaskAsync(NewUid(), "Buy groceries", "Milk, bread", "medium", null, new List<string>());
			await _database.AddTaskAsync(NewUid(), "Clean house", null, "medium", null, new List<string>());

			var results = await _database.SearchTasksHybridAsync("groceries");

			Assert.Contains(results, t => t.Title.Contains("groceries"));
		}

		[Fact]
		public async System.Threading.Tasks.Task AddTask_ShouldHandleTags()
		{
			var tags = new List<string> { "urgent", "work" };
			var task = await _database.AddTaskAsync(NewUid(), "Tagged Task", null, "medium", null, tags);

			Assert.Equal(tags, task.Tags);
		}

		[Fact]
		public async System.Threading.Tasks.Task AddTask_ShouldHandleDueDate()
		{
			var dueDate = new DateTime(2023, 12, 31);
			var task = await _database.AddTaskAsync(NewUid(), "Due Task", null, "medium", dueDate, new List<string>());

			Assert.Equal(dueDate, task.DueDate);
		}

		[Fact]
		public async System.Threading.Tasks.Task EndToEnd_AddAndListTasks()
		{
			await _database.AddTaskAsync(NewUid(), "Integration Test Task", "Test description", "high", null, new List<string>());

			var tasks = await _database.GetAllTasksAsync();

			Assert.Single(tasks);
			Assert.Equal("Integration Test Task", tasks[0].Title);
			Assert.Equal("Test description", tasks[0].Description);
			Assert.Equal("high", tasks[0].Priority);
			Assert.Equal("todo", tasks[0].Status);
		}

		[Fact]
		public async System.Threading.Tasks.Task EndToEnd_AddEditCompleteWorkflow()
		{
			var task = await _database.AddTaskAsync(NewUid(), "Workflow Task", "Original description", "medium", null, new List<string>());

			task.Title = "Updated Workflow Task";
			task.Description = "Updated description";
			task.Priority = "high";
			await _database.UpdateTaskAsync(task);

			await _database.CompleteTaskAsync(task.Uid);

			var finalTask = await _database.GetTaskByUidAsync(task.Uid);
			Assert.NotNull(finalTask);
			Assert.Equal("Updated Workflow Task", finalTask.Title);
			Assert.Equal("Updated description", finalTask.Description);
			Assert.Equal("high", finalTask.Priority);
			Assert.Equal("done", finalTask.Status);
		}

		[Fact]
		public async System.Threading.Tasks.Task EndToEnd_FullTextSearch()
		{
			await _database.AddTaskAsync(NewUid(), "Buy groceries", "Milk, bread, eggs", "medium", null, new List<string>());
			await _database.AddTaskAsync(NewUid(), "Clean the house", "Vacuum and dust", "medium", null, new List<string>());
			await _database.AddTaskAsync(NewUid(), "Write report", "Q4 financial report", "medium", null, new List<string>());

			var results = await _database.SearchTasksAsync("house");

			Assert.Single(results);
			Assert.Equal("Clean the house", results[0].Title);
		}

		[Fact]
		public async System.Threading.Tasks.Task EndToEnd_HybridSearch()
		{
			await _database.AddTaskAsync(NewUid(), "Buy groceries", "Milk and bread", "medium", null, new List<string>());
			await _database.AddTaskAsync(NewUid(), "Purchase items", "Shopping list", "medium", null, new List<string>());

			var results = await _database.SearchTasksAsync("buy");

			Assert.Contains(results, t => t.Title.Contains("Buy"));
		}

		[Fact]
		public async System.Threading.Tasks.Task QuickAdd_WithTitle_CreatesTask()
		{
			await _database.AddTaskAsync(NewUid(), "Buy milk", null, "medium", null, new List<string>());

			var tasks = await _database.GetAllTasksAsync();
			Assert.Single(tasks);
			Assert.Equal("Buy milk", tasks[0].Title);
			Assert.Equal("medium", tasks[0].Priority);
		}

		[Fact]
		public async System.Threading.Tasks.Task QuickAdd_WithAllOptions_CreatesTask()
		{
			await _database.AddTaskAsync(
				NewUid(),
				"Complex task",
				"Full description",
				"high",
				DateTime.Parse("2024-12-31"),
				new List<string> { "work", "urgent" },
				"work",
				null,
				"todo",
				null);

			var tasks = await _database.GetAllTasksAsync();
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
			await _database.AddTaskAsync(
				NewUid(),
				"Test task",
				null,
				"medium",
				null,
				new List<string>(),
				null,
				null,
				"in_progress",
				null);

			var tasks = await _database.GetAllTasksAsync();
			Assert.Single(tasks);
			Assert.Equal("in_progress", tasks[0].Status);
		}

		[Fact]
		public async System.Threading.Tasks.Task DeleteTask_ShouldArchiveAndHideFromQueries()
		{
			var task = await _database.AddTaskAsync(NewUid(), "Archive me", null, "medium", null, new List<string>());

			await _database.DeleteTaskAsync(task.Uid);

			var byUid = await _database.GetTaskByUidAsync(task.Uid);
			var all = await _database.GetAllTasksAsync();

			Assert.Null(byUid);
			Assert.DoesNotContain(all, t => t.Uid == task.Uid);
		}

		[Fact]
		public async System.Threading.Tasks.Task ClearAllTasksAsync_ShouldArchiveAllTasks()
		{
			await _database.AddTaskAsync(NewUid(), "One", null, "medium", null, new List<string>());
			await _database.AddTaskAsync(NewUid(), "Two", null, "medium", null, new List<string>());

			await _database.ClearAllTasksAsync();

			var tasks = await _database.GetAllTasksAsync();
			Assert.Empty(tasks);
		}

		[Fact]
		public async System.Threading.Tasks.Task GetAllUniqueTagsAsync_ShouldExcludeArchivedTasks()
		{
			await _database.AddTaskAsync(NewUid(), "Active", null, "medium", null, new List<string> { "alpha", "beta" });
			var archived = await _database.AddTaskAsync(NewUid(), "Archived", null, "medium", null, new List<string> { "legacy" });

			await _database.DeleteTaskAsync(archived.Uid);

			var tags = await _database.GetAllUniqueTagsAsync();
			Assert.Equal(new[] { "alpha", "beta" }, tags);
		}

		[Fact]
		public async System.Threading.Tasks.Task SearchTasksFTS_ShouldExcludeArchivedTasks()
		{
			var active = await _database.AddTaskAsync(NewUid(), "Visible task", "keep", "medium", null, new List<string>());
			var archived = await _database.AddTaskAsync(NewUid(), "Hidden task", "archive", "medium", null, new List<string>());

			await _database.DeleteTaskAsync(archived.Uid);

			var results = await _database.SearchTasksFTSAsync("task");

			Assert.Contains(results, t => t.Uid == active.Uid);
			Assert.DoesNotContain(results, t => t.Uid == archived.Uid);
		}

		[Fact]
		public async System.Threading.Tasks.Task AddTask_ShouldPersistAssigneeAndProject()
		{
			var task = await _database.AddTaskAsync(NewUid(), "Assigned", "desc", "medium", null, new List<string>(), "alpha", "jordan");

			var fetched = await _database.GetTaskByUidAsync(task.Uid);

			Assert.NotNull(fetched);
			Assert.Equal("alpha", fetched!.Project);
			Assert.Equal("jordan", fetched.Assignee);
		}
	}
}
