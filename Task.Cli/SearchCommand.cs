using Spectre.Console;
using Spectre.Console.Cli;
using System.ComponentModel;
using System.Threading;

namespace TaskApp
{
    [Description("Search for tasks using full-text, semantic, or hybrid search. Use --json for structured results.")]
    public class SearchCommand : AsyncCommand<SearchCommand.Settings>
    {
        public class Settings : Program.TaskCommandSettings
        {
            [CommandArgument(0, "<query>")]
            [Description("The search query string (e.g., 'groceries' or 'urgent tasks')")]
            public string? Query { get; set; }

            [CommandOption("--type")]
            [Description("Search type: fts (full-text), semantic, or hybrid (default: fts)")]
            public string Type { get; set; } = "fts";
        }

        public override async Task<int> ExecuteAsync(CommandContext context, Settings settings, CancellationToken cancellationToken)
        {
            var service = await Program.GetTaskServiceAsync(settings, cancellationToken);

            if (string.IsNullOrEmpty(settings.Query))
            {
                ErrorHelper.ShowError(
                    "Query is required.",
                    "task search 'groceries'",
                    "task search --help");
                return 1;
            }

            var tasks = await service.SearchTasksAsync(settings.Query, settings.Type, cancellationToken);

            if (settings.Json)
            {
#pragma warning disable IL2026
                Console.WriteLine(System.Text.Json.JsonSerializer.Serialize(tasks, JsonHelper.Options));
#pragma warning restore IL2026
            }
            else
            {
                var table = new Table();
                table.AddColumn("ID");
                table.AddColumn("Title");
                table.AddColumn("Priority");
                table.AddColumn("Status");

                foreach (var task in tasks)
                {
                    table.AddRow(task.Uid, task.Title, task.Priority, task.Status);
                }

                AnsiConsole.Write(table);
            }

            return 0;
        }
    }
}