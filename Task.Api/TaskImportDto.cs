using System.Text.Json.Serialization;

namespace Task.Api;

public class TaskImportDto
{
    public required string Title { get; set; }
    public string? Description { get; set; }
    public string? Priority { get; set; }
    [JsonConverter(typeof(DateTimeNullableConverter))]
    public DateTime? DueDate { get; set; }
    public List<string>? Tags { get; set; }
    public string? Status { get; set; }
}