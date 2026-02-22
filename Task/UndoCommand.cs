using Spectre.Console;
using Spectre.Console.Cli;
using System.ComponentModel;
using System.Threading;

namespace TaskApp
{
    [Description("Undo the last action (complete, delete, create, or edit). Use --json for structured output.")]
    public class UndoCommand : AsyncCommand<UndoCommand.Settings>
    {
        public class Settings : Program.TaskCommandSettings
        {
            [CommandOption("--list")]
            [Description("Show the available undo actions")]
            public bool List { get; set; }
        }

        public override async Task<int> ExecuteAsync(CommandContext context, Settings settings, CancellationToken cancellationToken)
        {
            var service = await Program.GetTaskServiceAsync(settings, cancellationToken);

            var undoStack = service.GetUndoStack();

            if (settings.List)
            {
                if (undoStack.Count == 0)
                {
                    Console.WriteLine("No actions to undo.");
                    return 0;
                }

                if (settings.Json)
                {
#pragma warning disable IL2026
                    var actions = undoStack.Select((a, i) => new { index = i + 1, type = a.Type.ToString(), uid = a.Uid }).ToList();
                    Console.WriteLine(System.Text.Json.JsonSerializer.Serialize(actions, JsonHelper.Options));
#pragma warning restore IL2026
                }
                else
                {
                    var table = new Table();
                    table.AddColumn("Index");
                    table.AddColumn("Action");
                    table.AddColumn("Task ID");

                    for (int i = 0; i < undoStack.Count; i++)
                    {
                        table.AddRow((i + 1).ToString(), undoStack[i].Type.ToString(), undoStack[i].Uid);
                    }

                    AnsiConsole.Write(table);
                }
                return 0;
            }

            if (undoStack.Count == 0)
            {
                Console.Error.WriteLine("ERROR: No actions to undo.");
                return 1;
            }

            var lastAction = undoStack[^1];
            var success = await service.UndoLastActionAsync(cancellationToken);

            if (success)
            {
                if (settings.Json)
                {
#pragma warning disable IL2026
                    Console.WriteLine(System.Text.Json.JsonSerializer.Serialize(new { undone = true, action = lastAction.Type.ToString(), uid = lastAction.Uid }, JsonHelper.Options));
#pragma warning restore IL2026
                }
                else
                {
                    Console.WriteLine($"Undone: {lastAction.Type} on task {lastAction.Uid}");
                }
                return 0;
            }
            else
            {
                Console.Error.WriteLine("ERROR: Failed to undo the last action.");
                return 1;
            }
        }
    }
}
