using Xunit;
using TaskApp;
using System.IO;
using System;

namespace TaskApp.Tests.IntegrationTests
{
    public class AddCommandTests : IDisposable
    {
        private readonly string _testDbPath;

        public AddCommandTests()
        {
            _testDbPath = $"/tmp/test_task_add_{Guid.NewGuid()}.db";
        }

        public void Dispose()
        {
            if (File.Exists(_testDbPath))
            {
                File.Delete(_testDbPath);
            }
        }

        [Fact]
        public async System.Threading.Tasks.Task QuickAdd_WithTitle_CreatesTask()
        {
            ITaskService db = new Database(_testDbPath);
            await db.InitializeAsync();

            var task = await db.AddTaskAsync("Buy milk", null, "medium", null, new List<string>());

            var tasks = await db.GetAllTasksAsync();
            Assert.Single(tasks);
            Assert.Equal("Buy milk", tasks[0].Title);
            Assert.Equal("medium", tasks[0].Priority);
        }

        [Fact]
        public async System.Threading.Tasks.Task QuickAdd_WithAllOptions_CreatesTask()
        {
            ITaskService db = new Database(_testDbPath);
            await db.InitializeAsync();

            var task = await db.AddTaskAsync(
                "Complex task",
                "Full description",
                "high",
                DateTime.Parse("2024-12-31"),
                new List<string> { "work", "urgent" },
                "work",
                new List<string>(),
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
        public void ValidatePriority_WithInvalidPriority_ReturnsFalse()
        {
            var result = ErrorHelper.ValidatePriority("urgent", out var errorMessage);

            Assert.False(result);
            Assert.Contains("not a valid priority", errorMessage);
            Assert.Contains("task add --help", errorMessage);
        }

        [Fact]
        public void ValidatePriority_WithValidPriority_ReturnsTrue()
        {
            var result = ErrorHelper.ValidatePriority("high", out var errorMessage);

            Assert.True(result);
            Assert.Null(errorMessage);
        }

        [Fact]
        public void ValidateStatus_WithInvalidStatus_ReturnsFalse()
        {
            var result = ErrorHelper.ValidateStatus("pending", out var errorMessage);

            Assert.False(result);
            Assert.Contains("not a valid status", errorMessage);
            Assert.Contains("task add --help", errorMessage);
        }

        [Fact]
        public void ValidateStatus_WithValidStatus_ReturnsTrue()
        {
            var result = ErrorHelper.ValidateStatus("in_progress", out var errorMessage);

            Assert.True(result);
            Assert.Null(errorMessage);
        }

        [Fact]
        public void ValidateDate_WithInvalidDate_ReturnsFalse()
        {
            var result = ErrorHelper.ValidateDate("not-a-date", out var errorMessage);

            Assert.False(result);
            Assert.Contains("not a valid date", errorMessage);
            Assert.Contains("task add --help", errorMessage);
        }

        [Fact]
        public void ValidateDate_WithValidDate_ReturnsTrue()
        {
            var result = ErrorHelper.ValidateDate("2024-12-31", out var errorMessage);

            Assert.True(result);
            Assert.Null(errorMessage);
        }

        [Fact]
        public async System.Threading.Tasks.Task QuickAdd_WithValidStatus_CreatesTask()
        {
            ITaskService db = new Database(_testDbPath);
            await db.InitializeAsync();

            var task = await db.AddTaskAsync(
                "Test task",
                null,
                "medium",
                null,
                new List<string>(),
                null,
                new List<string>(),
                "in_progress",
                default);

            var tasks = await db.GetAllTasksAsync();
            Assert.Single(tasks);
            Assert.Equal("in_progress", tasks[0].Status);
        }
    }
}
