using Spectre.Console;
using Spectre.Console.Cli;
using System.ComponentModel;
using System.Threading;

namespace Task.Cli
{
    [Description("List all tasks in a table format. Use filters to narrow results. Use --json for structured output suitable for LLMs.")]
    public class ListCommand : AsyncCommand<ListCommand.Settings>
    {
        public class Settings : Program.TaskCommandSettings
        {
            [CommandOption("--status")]
            [Description("Filter tasks by status: todo, done, in_progress, or blocked (e.g., --status blocked)")]
            public string? Status { get; set; }

            [CommandOption("--priority")]
            [Description("Filter tasks by priority: high, medium, or low (e.g., --priority high)")]
            public string? Priority { get; set; }

            [CommandOption("--project")]
            [Description("Filter tasks by project name (e.g., --project work)")]
            public string? Project { get; set; }

            [CommandOption("--assignee")]
            [Description("Filter tasks by assignee name (e.g., --assignee john.doe)")]
            public string? Assignee { get; set; }

            [CommandOption("--tags")]
            [Description("Filter tasks by comma-separated tags (e.g., --tags urgent,work)")]
            public string? Tags { get; set; }

            [CommandOption("--due-before")]
            [Description("Filter tasks due on or before this date (YYYY-MM-DD, e.g., --due-before 2024-04-01)")]
            public string? DueBefore { get; set; }

            [CommandOption("--due-after")]
            [Description("Filter tasks due on or after this date (YYYY-MM-DD, e.g., --due-after 2024-04-01)")]
            public string? DueAfter { get; set; }

            [CommandOption("--sort")]
            [Description("Sort tasks by field: priority, due, created (e.g., --sort due)")]
            public string? Sort { get; set; }
        }

        public override async Task<int> ExecuteAsync(CommandContext context, Settings settings, CancellationToken cancellationToken)
        {
            var status = settings.Status;
            if (!string.IsNullOrEmpty(status))
            {
                status = status.ToLower() switch
                {
                    "pending" => "todo",
                    "completed" => "done",
                    _ => status
                };
            }

            DateTime? dueBefore = null;
            if (!string.IsNullOrEmpty(settings.DueBefore))
            {
                if (!DateTime.TryParse(settings.DueBefore, out var parsedBefore))
                {
                    ErrorHelper.ShowError(
                        $"'{settings.DueBefore}' is not a valid date. Use YYYY-MM-DD format.",
                        "task list --due-before 2024-04-01",
                        "task list --help");
                    return 1;
                }
                dueBefore = parsedBefore;
            }

            DateTime? dueAfter = null;
            if (!string.IsNullOrEmpty(settings.DueAfter))
            {
                if (!DateTime.TryParse(settings.DueAfter, out var parsedAfter))
                {
                    ErrorHelper.ShowError(
                        $"'{settings.DueAfter}' is not a valid date. Use YYYY-MM-DD format.",
                        "task list --due-after 2024-04-01",
                        "task list --help");
                    return 1;
                }
                dueAfter = parsedAfter;
            }

            var service = await Program.GetTaskServiceAsync(settings, cancellationToken);
            var tasks = await service.GetAllTasksAsync(
                status: status,
                priority: settings.Priority,
                project: settings.Project,
                assignee: settings.Assignee,
                tags: settings.Tags,
                dueBefore: dueBefore,
                dueAfter: dueAfter,
                sortBy: settings.Sort,
                cancellationToken: cancellationToken);

            if (settings.Json)
            {
#pragma warning disable IL2026
                Console.WriteLine(System.Text.Json.JsonSerializer.Serialize(tasks, JsonHelper.Options));
#pragma warning restore IL2026
            }
            else
            {
                var table = new Table();
                table.AddColumn("UID");
                table.AddColumn("Title");
                table.AddColumn("Project");
                table.AddColumn("Assignee");
                table.AddColumn("Priority");
                table.AddColumn("Status");
                table.AddColumn("Due Date");
                table.AddColumn("Depends On");

                foreach (var task in tasks)
                {
                    table.AddRow(
                        task.Uid,
                        task.Title,
                        task.Project ?? "-",
                        task.Assignee ?? "-",
                        task.Priority,
                        task.Status,
                        task.DueDateString,
                        task.DependsOn.Count > 0 ? string.Join(", ", task.DependsOn) : "-");
                }

                AnsiConsole.Write(table);
            }

            return 0;
        }
    }
}
