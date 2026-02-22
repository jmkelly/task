using System.Text.Json.Serialization;
using Task.Core;

namespace Task.Cli;

[JsonSerializable(typeof(TaskItem))]
[JsonSerializable(typeof(List<TaskItem>))]
[JsonSerializable(typeof(Dictionary<string, object>))]
[JsonSerializable(typeof(Config))]
[JsonSourceGenerationOptions(WriteIndented = true)]
public partial class TaskJsonContext : JsonSerializerContext
{
}