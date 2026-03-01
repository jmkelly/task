namespace Task.Core.Providers.Telegram;

public sealed record TelegramSendMessageRequest(string chat_id, string text);
