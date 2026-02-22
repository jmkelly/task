using System;
using System.Text.Json.Serialization;

namespace Task.Core
{
	public class TaskItem
	{
		public int Id { get; set; }
		public required string Uid { get; set; }
		public required string Title { get; set; }
		public string? Description { get; set; }
		public required string Priority { get; set; } = "medium"; // high, medium, low
		public DateTime? DueDate { get; set; }
		public List<string> Tags { get; set; } = new();
		[JsonPropertyName("project")]
		public string? Project { get; set; }
		public List<string> DependsOn { get; set; } = new();
		public required string Status { get; set; } = "todo"; // todo, in_progress, done
		public required DateTime CreatedAt { get; set; }
		public required DateTime UpdatedAt { get; set; }

		// For JSON serialization
		public string DueDateString => DueDate?.ToString("yyyy-MM-dd") ?? "";
		public string TagsString => string.Join(",", Tags);
		public string DependsOnString => string.Join(",", DependsOn);
	}
}