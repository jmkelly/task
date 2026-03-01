using System.Threading;

namespace Task.Core.Providers.Telegram;

public interface ITelegramProvider
{
    System.Threading.Tasks.Task SendMessageAsync(string message, CancellationToken cancellationToken = default);
}
