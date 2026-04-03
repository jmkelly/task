using System.Text.Json.Serialization;
using Task.Core;

namespace Task.Cli;

[JsonSerializable(typeof(TaskItem))]
[JsonSerializable(typeof(List<TaskItem>))]
[JsonSerializable(typeof(Dictionary<string, object>))]
[JsonSerializable(typeof(Config))]
[JsonSerializable(typeof(Config.DatabaseConfig))]
[JsonSerializable(typeof(Config.SqliteConfig))]
[JsonSerializable(typeof(Config.PostgresConfig))]
[JsonSerializable(typeof(Config.TelegramConfig))]
[JsonSerializable(typeof(ServerState))]
[JsonSourceGenerationOptions(WriteIndented = true)]
public partial class TaskJsonContext : JsonSerializerContext
{
}
