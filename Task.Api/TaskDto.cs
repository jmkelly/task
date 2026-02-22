using System.Text.Json.Serialization;

namespace Task.Api;

public class TaskDto
{
    public int Id { get; set; }
    public required string Uid { get; set; }
    public required string Title { get; set; }
    public string? Description { get; set; }
    public required string Priority { get; set; }
    [JsonConverter(typeof(DateTimeNullableConverter))]
    public DateTime? DueDate { get; set; }
    public List<string> Tags { get; set; } = new();
    public required string Status { get; set; }
    public required DateTime CreatedAt { get; set; }
    public required DateTime UpdatedAt { get; set; }
}