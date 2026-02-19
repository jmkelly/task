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
            var db = await Program.GetDatabaseAsync(settings, cancellationToken);

            if (string.IsNullOrEmpty(settings.Query))
            {
                Console.Error.WriteLine("ERROR: Query is required.");
                return 1;
            }

            List<TaskItem> tasks;
            if (settings.Type == "fts")
            {
                // Use FTS search
                tasks = await db.SearchTasksFTS(settings.Query, cancellationToken);
            }
            else if (settings.Type == "semantic")
            {
                // Use semantic search
                tasks = await db.SearchTasksSemantic(settings.Query, cancellationToken);
            }
            else if (settings.Type == "hybrid")
            {
                // Use hybrid search
                tasks = await db.SearchTasksHybrid(settings.Query, cancellationToken);
            }
            else
            {
                // Simple search
                tasks = (await db.GetAllTasks(cancellationToken)).Where(t => 
                    t.Title.Contains(settings.Query, StringComparison.OrdinalIgnoreCase) ||
                    (t.Description ?? "").Contains(settings.Query, StringComparison.OrdinalIgnoreCase) ||
                    t.Tags.Any(tag => tag.Contains(settings.Query, StringComparison.OrdinalIgnoreCase))
                ).ToList();
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