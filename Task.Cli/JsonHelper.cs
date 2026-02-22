using System.Text.Json;
using System.Text.Json.Serialization;

namespace Task.Cli
{
    public static class JsonHelper
    {
        public static JsonSerializerOptions Options { get; } = new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            WriteIndented = true
        };
    }
}
