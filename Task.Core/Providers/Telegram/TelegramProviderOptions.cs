namespace Task.Core.Providers.Telegram;

public sealed class TelegramProviderOptions
{
    public string BotToken { get; init; } = string.Empty;
    public string ChatId { get; init; } = string.Empty;
    public string DefaultMessage { get; init; } = "No tasks are currently in todo or in_progress.";
    public bool Enabled { get; init; } = false;
}
