using Spectre.Console.Cli;
using System.ComponentModel;
using System.Threading;

namespace TaskApp
{
    [Description("Edit an existing task's properties. Specify the task ID and any fields to update. Use --json for LLM-friendly output.")]
    public class EditCommand : AsyncCommand<EditCommand.Settings>
    {
        public class Settings : Program.TaskCommandSettings
        {
            [CommandArgument(0, "<id>")]
            [Description("The unique ID of the task to edit (e.g., 'a2b3k9')")]
            public string? Id { get; set; }

            [CommandOption("-t|--title")]
            [Description("Update the task title (e.g., 'Updated title')")]
            public string? Title { get; set; }

            [CommandOption("-d|--description")]
            [Description("Update the task description (e.g., 'Detailed steps for completion')")]
            public string? Description { get; set; }

            [CommandOption("-p|--priority")]
            [Description("Update the priority: high, medium, or low (e.g., 'high')")]
            public string? Priority { get; set; }

            [CommandOption("--due-date")]
            [Description("Update the due date in YYYY-MM-DD format (e.g., '2024-02-20')")]
            public string? DueDate { get; set; }

            [CommandOption("--tags")]
            [Description("Update the comma-separated tags (e.g., 'work,urgent')")]
            public string? Tags { get; set; }
        }

        public override async Task<int> ExecuteAsync(CommandContext context, Settings settings, CancellationToken cancellationToken)
        {
            var db = await Program.GetDatabaseAsync(settings, cancellationToken);

            if (string.IsNullOrEmpty(settings.Id))
            {
                Console.Error.WriteLine("ERROR: ID is required.");
                return 1;
            }

            var task = await db.GetTaskByUid(settings.Id, cancellationToken);

            if (task == null)
            {
                Console.Error.WriteLine($"ERROR: Task with ID {settings.Id} not found.");
                return 1;
            }

            if (!string.IsNullOrEmpty(settings.Title)) task.Title = settings.Title;
            if (!string.IsNullOrEmpty(settings.Description)) task.Description = settings.Description;
            if (!string.IsNullOrEmpty(settings.Priority)) task.Priority = settings.Priority;
            if (!string.IsNullOrEmpty(settings.DueDate))
            {
                if (DateTime.TryParse(settings.DueDate, out var parsedDate))
                {
                    task.DueDate = parsedDate;
                }
                else
                {
                    Console.Error.WriteLine("ERROR: Invalid date format. Use YYYY-MM-DD.");
                    return 1;
                }
            }
            if (!string.IsNullOrEmpty(settings.Tags))
            {
                task.Tags = settings.Tags.Split(',').Select(t => t.Trim()).ToList();
            }

            await db.UpdateTask(task, cancellationToken);

            if (settings.Json)
            {
#pragma warning disable IL2026
                Console.WriteLine(System.Text.Json.JsonSerializer.Serialize(task));
#pragma warning restore IL2026
            }
            else
            {
                Console.WriteLine($"Task {task.Id} updated.");
            }

            return 0;
        }
    }
}