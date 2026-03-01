using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Task.Core.Providers.Telegram;

public sealed class TelegramNotificationService
{
    private static readonly HashSet<string> TriggerStatuses =
        new(StringComparer.OrdinalIgnoreCase) { "todo", "in_progress" };

    private readonly ITelegramProvider _provider;
    private readonly IOptions<TelegramProviderOptions> _options;
    private readonly ILogger<TelegramNotificationService> _logger;

    public TelegramNotificationService(
        ITelegramProvider provider,
        IOptions<TelegramProviderOptions> options,
        ILogger<TelegramNotificationService> logger)
    {
        _provider = provider;
        _options = options;
        _logger = logger;
    }

    public async System.Threading.Tasks.Task NotifyWhenNoActiveTasksAsync(
        IReadOnlyCollection<TaskItem> tasks,
        CancellationToken cancellationToken = default)
    {
        if (tasks == null)
        {
            throw new ArgumentNullException(nameof(tasks));
        }

        var activeCount = tasks.Count(task => TriggerStatuses.Contains(task.Status));
        if (activeCount > 0)
        {
            _logger.LogInformation(
                "Telegram notification not sent. Active tasks count: {ActiveCount}.",
                activeCount);
            return;
        }

        var message = _options.Value.DefaultMessage;
        if (string.IsNullOrWhiteSpace(message))
        {
            message = "No tasks are currently in todo or in_progress.";
        }

        _logger.LogInformation("Telegram notification triggered. Active tasks count is zero.");
        await _provider.SendMessageAsync(message, cancellationToken);
    }
}
