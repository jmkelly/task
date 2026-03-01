using System.Text.Json.Serialization;

namespace Task.Core;

// DTOs for API communication (simplified versions)
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
    public string? Project { get; set; }
    public string? Assignee { get; set; }
    public List<string> DependsOn { get; set; } = new();
    public string? Status { get; set; }
    public bool Archived { get; set; }
    [JsonConverter(typeof(DateTimeNullableConverter))]
    public DateTime? ArchivedAt { get; set; }
    public required DateTime CreatedAt { get; set; }
    public required DateTime UpdatedAt { get; set; }
}

public class TaskCreateDto
{
    public required string Title { get; set; }
    public string? Description { get; set; }
    public required string Priority { get; set; }
    [JsonConverter(typeof(DateTimeNullableConverter))]
    public DateTime? DueDate { get; set; }
    public List<string> Tags { get; set; } = new();
    public string? Project { get; set; }
    public string? Assignee { get; set; }
    public List<string>? DependsOn { get; set; }
    public string? Status { get; set; }
    public bool? Archived { get; set; }
    [JsonConverter(typeof(DateTimeNullableConverter))]
    public DateTime? ArchivedAt { get; set; }
}

public class TaskUpdateDto
{
    public string? Title { get; set; }
    public string? Description { get; set; }
    public string? Priority { get; set; }
    [JsonConverter(typeof(DateTimeNullableConverter))]
    public DateTime? DueDate { get; set; }
    public List<string>? Tags { get; set; }
    public string? Project { get; set; }
    public string? Assignee { get; set; }
    public List<string>? DependsOn { get; set; }
    public string? Status { get; set; }
    public bool? Archived { get; set; }
    [JsonConverter(typeof(DateTimeNullableConverter))]
    public DateTime? ArchivedAt { get; set; }
}

public class TaskImportDto
{
    public required string Title { get; set; }
    public string? Description { get; set; }
    public string? Priority { get; set; }
    [JsonConverter(typeof(DateTimeNullableConverter))]
    public DateTime? DueDate { get; set; }
    public List<string>? Tags { get; set; }
    public string? Project { get; set; }
    public string? Assignee { get; set; }
    public List<string>? DependsOn { get; set; }
    public string? Status { get; set; }
    public bool? Archived { get; set; }
    [JsonConverter(typeof(DateTimeNullableConverter))]
    public DateTime? ArchivedAt { get; set; }
}
