namespace Task.Core.Providers.Telegram;

public sealed class TelegramProviderOptions
{
    public string BotToken { get; set; } = string.Empty;
    public string ChatId { get; set; } = string.Empty;
    public string DefaultMessage { get; set; } = "No tasks are currently in todo or in_progress.";
    public bool Enabled { get; set; } = false;
}
