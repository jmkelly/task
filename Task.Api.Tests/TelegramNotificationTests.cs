using System;
using System.Collections.Generic;
using System.Threading;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Task.Core;
using Task.Core.Providers.Telegram;
using Xunit;
using SystemTask = System.Threading.Tasks.Task;

namespace Task.Api.Tests.UnitTests
{
    public class TelegramNotificationTests
    {
        [Fact]
        public async SystemTask NotifyWhenNoActiveTasksAsync_SendsMessage_WhenNoTodoOrInProgress()
        {
            var options = Options.Create(new TelegramProviderOptions
            {
                DefaultMessage = "No active tasks right now.",
                Enabled = true,
                BotToken = "token",
                ChatId = "chat"
            });
            var provider = new FakeTelegramProvider();
            var service = new TelegramNotificationService(provider, options, NullLogger<TelegramNotificationService>.Instance);

            var tasks = new List<TaskItem>
            {
                BuildTask("done")
            };

            await service.NotifyWhenNoActiveTasksAsync(tasks, CancellationToken.None);

            Assert.Single(provider.Messages);
            Assert.Equal("No active tasks right now.", provider.Messages[0]);
        }

        [Fact]
        public async SystemTask NotifyWhenNoActiveTasksAsync_SkipsMessage_WhenTodoExists()
        {
            var options = Options.Create(new TelegramProviderOptions
            {
                DefaultMessage = "Should not send.",
                Enabled = true,
                BotToken = "token",
                ChatId = "chat"
            });
            var provider = new FakeTelegramProvider();
            var service = new TelegramNotificationService(provider, options, NullLogger<TelegramNotificationService>.Instance);

            var tasks = new List<TaskItem>
            {
                BuildTask("todo"),
                BuildTask("done")
            };

            await service.NotifyWhenNoActiveTasksAsync(tasks, CancellationToken.None);

            Assert.Empty(provider.Messages);
        }

        private static TaskItem BuildTask(string status)
        {
            return new TaskItem
            {
                Id = 1,
                Uid = Guid.NewGuid().ToString("N").Substring(0, 6),
                Title = "Test",
                Description = null,
                Priority = "medium",
                DueDate = null,
                Tags = new List<string>(),
                Project = null,
                Assignee = null,
                Status = status,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow,
                Archived = false,
                ArchivedAt = null
            };
        }

        private sealed class FakeTelegramProvider : ITelegramProvider
        {
            public List<string> Messages { get; } = new();

            public SystemTask SendMessageAsync(string message, CancellationToken cancellationToken = default)
            {
                Messages.Add(message);
                return SystemTask.CompletedTask;
            }
        }
    }
}
