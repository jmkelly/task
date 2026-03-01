using System;
using System.Linq;
using System.Threading;
using System.Collections.Generic;
using Xunit;
using Task.Core;
using System.IO;

namespace Task.Api.Tests.UnitTests
{
    public class TaskServiceTests
    {
        [Fact]
        public async System.Threading.Tasks.Task ArchiveAllTasksAsync_ArchivesEveryTask()
        {
            // Arrange
            var dbPath = Path.Combine(Path.GetTempPath(), $"unit_test_{Guid.NewGuid()}.db");
            try
            {
                var service = new TaskService(dbPath);
                await service.InitializeAsync();

                // Add multiple unarchived tasks
                await service.AddTaskAsync("Task 1", "desc", "medium", null, new List<string>());
                await service.AddTaskAsync("Task 2", null, "low", null, new List<string>());
                await service.AddTaskAsync("Task 3", null, "high", null, new List<string>());

                // Confirm none are archived
                var pre = await service.GetAllTasksAsync();
                Assert.All(pre, t => Assert.False(t.Archived));

                // Act
                await service.ArchiveAllTasksAsync();

                // Assert
                var post = await service.GetAllTasksAsync();
                Assert.Empty(post); // All tasks should be hidden after archiving.
            }
            finally
            {
                try { File.Delete(dbPath); } catch { }
            }
        }
    }
}
