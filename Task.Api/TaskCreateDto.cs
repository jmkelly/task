using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;

namespace Task.Api;

public class TaskCreateDto
{
    [Required]
    [StringLength(200, MinimumLength = 1)]
    public required string Title { get; set; }

    public string? Description { get; set; }

    [Required]
    public required string Priority { get; set; } = "medium";

    [JsonConverter(typeof(DateTimeNullableConverter))]
    public DateTime? DueDate { get; set; }

    public List<string> Tags { get; set; } = new();

    public string? Project { get; set; }
}