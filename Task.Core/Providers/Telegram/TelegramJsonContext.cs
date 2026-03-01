using System.Text.Json.Serialization;

namespace Task.Core.Providers.Telegram;

[JsonSerializable(typeof(TelegramSendMessageRequest))]
public sealed partial class TelegramJsonContext : JsonSerializerContext
{
}
