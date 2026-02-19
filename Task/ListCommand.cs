using Spectre.Console;
using Spectre.Console.Cli;
using System.ComponentModel;
using System.Threading;

namespace TaskApp
{
    [Description("List all tasks in a table format. Use filters to narrow results. Use --json for structured output suitable for LLMs.")]
    public class ListCommand : AsyncCommand<ListCommand.Settings>
    {
        public class Settings : Program.TaskCommandSettings
        {
            [CommandOption("--status")]
            [Description("Filter tasks by status: pending or completed (e.g., --status pending)")]
            public string? Status { get; set; }

            [CommandOption("--priority")]
            [Description("Filter tasks by priority: high, medium, or low (e.g., --priority high)")]
            public string? Priority { get; set; }
        }

        public override async Task<int> ExecuteAsync(CommandContext context, Settings settings, CancellationToken cancellationToken)
        {
            var db = await Program.GetDatabaseAsync(settings, cancellationToken);
            var tasks = await db.GetAllTasks(cancellationToken);

            // Filter
            if (!string.IsNullOrEmpty(settings.Status))
            {
                tasks = tasks.Where(t => t.Status == settings.Status).ToList();
            }
            if (!string.IsNullOrEmpty(settings.Priority))
            {
                tasks = tasks.Where(t => t.Priority == settings.Priority).ToList();
            }

            if (settings.Json)
            {
#pragma warning disable IL2026
                Console.WriteLine(System.Text.Json.JsonSerializer.Serialize(tasks));
#pragma warning restore IL2026
            }
            else
            {
                var table = new Table();
                table.AddColumn("ID");
                table.AddColumn("Title");
                table.AddColumn("Priority");
                table.AddColumn("Status");
                table.AddColumn("Due Date");

                foreach (var task in tasks)
                {
                    table.AddRow(task.Uid, task.Title, task.Priority, task.Status, task.DueDateString);
                }

                AnsiConsole.Write(table);
            }

            return 0;
        }
    }
}