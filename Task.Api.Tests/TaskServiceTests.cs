using System;
using System.Collections.Generic;
using System.IO;
using Task.Core;
using Xunit;
using SystemTask = System.Threading.Tasks.Task;

namespace Task.Api.Tests.UnitTests
{
    public class TaskServiceTests
    {
        [Fact]
        public async SystemTask ArchiveAllTasksAsync_ArchivesEveryTask()
        {
            var dbPath = Path.Combine(Path.GetTempPath(), $"unit_test_{Guid.NewGuid()}.db");
            try
            {
                var service = new TaskService(dbPath);
                await service.InitializeAsync();

                await service.AddTaskAsync("Task 1", "desc", "medium", null, new List<string>());
                await service.AddTaskAsync("Task 2", null, "low", null, new List<string>());
                await service.AddTaskAsync("Task 3", null, "high", null, new List<string>());

                var pre = await service.GetAllTasksAsync();
                Assert.All(pre, t => Assert.False(t.Archived));

                await service.ArchiveAllTasksAsync();

                var post = await service.GetAllTasksAsync();
                Assert.Empty(post);
            }
            finally
            {
                try { File.Delete(dbPath); } catch { }
            }
        }
    }
}
