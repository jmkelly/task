using Spectre.Console.Cli;
using System.ComponentModel;
using System.Threading;

namespace Task.Cli
{
	[Description("Mark a task as completed. Use --json for structured confirmation output.")]
	public class CompleteCommand : AsyncCommand<CompleteCommand.Settings>
	{
		public class Settings : Program.TaskCommandSettings
		{
			[CommandArgument(0, "[ids]")]
			[Description("The 6-character alpha UID(s) of the task(s) to mark as completed (e.g., 'a2b3k9' or 'a2b3k9 d4e5f6')")]
			public string[] Ids { get; set; } = Array.Empty<string>();

			[CommandOption("--all")]
			[Description("Mark all incomplete tasks as completed")]
			public bool All { get; set; }
		}

		public override async Task<int> ExecuteAsync(CommandContext context, Settings settings, CancellationToken cancellationToken)
		{
			var service = await Program.GetTaskServiceAsync(settings, cancellationToken);
			var idsToComplete = new List<string>();

			if (settings.All)
			{
				var allTasks = await service.GetAllTasksAsync(status: "todo", cancellationToken: cancellationToken);
				if (allTasks.Count == 0)
				{
					Console.WriteLine("No incomplete tasks to complete.");
					return 0;
				}
				idsToComplete = allTasks.Select(t => t.Uid).ToList();
			}
			else
			{
				if (settings.Ids.Length == 0)
				{
					ErrorHelper.ShowError(
						"Task UID is required. (Provide at least one 6-character alpha UID, e.g., a2b3k9)",
						"task complete <uid> or task complete --all (UID is a 6-char code, e.g., a2b3k9)",
						"task complete --help");
					return 1;
				}
				// Ensure all IDs look like UIDs
				idsToComplete = settings.Ids.ToList(); // Should be 6-character UIDs.
			}

			var completed = new List<string>();
			var failed = new List<string>();
			var warnings = new List<string>();

			foreach (var id in idsToComplete)
			{

				await service.CompleteTaskAsync(id, cancellationToken);
				completed.Add(id);
			}

			if (settings.Json)
			{
#pragma warning disable IL2026
				Console.WriteLine(System.Text.Json.JsonSerializer.Serialize(new { completed = completed.Count, ids = completed, failed = failed, warnings = warnings }, JsonHelper.Options));
#pragma warning restore IL2026
			}
			else
			{
				if (warnings.Count > 0)
				{
					foreach (var warning in warnings)
					{
						Console.Error.WriteLine($"WARNING: {warning}");
					}
				}
				if (completed.Count > 0)
				{
					Console.WriteLine($"Task(s) {string.Join(", ", completed)} marked as completed.");
				}
				if (failed.Count > 0)
				{
					ErrorHelper.ShowError($"Task UID(s) not found: {string.Join(", ", failed)}");
				}
			}

			return failed.Count > 0 && completed.Count == 0 ? 1 : 0;
		}
	}
}
