using Spectre.Console.Cli;
using System.ComponentModel;
using System.Threading;

namespace Task.Cli
{
    [Description("Reset a task's status back to todo. Use --json for structured confirmation output.")]
    public class ResetCommand : AsyncCommand<ResetCommand.Settings>
    {
        public class Settings : Program.TaskCommandSettings
        {
            [CommandArgument(0, "<id>")]
            [Description("The unique ID of the task to reset (e.g., 'a2b3k9')")]
            public string? Id { get; set; }

            [CommandOption("--all")]
            [Description("Reset all done tasks to todo")]
            public bool All { get; set; }
        }

        public override async Task<int> ExecuteAsync(CommandContext context, Settings settings, CancellationToken cancellationToken)
        {
            var service = await Program.GetTaskServiceAsync(settings, cancellationToken);

            if (settings.All)
            {
                var allTasks = await service.GetAllTasksAsync(status: "done", cancellationToken: cancellationToken);
                if (allTasks.Count == 0)
                {
                    Console.WriteLine("No done tasks to reset.");
                    return 0;
                }

                foreach (var task in allTasks)
                {
                    task.Status = "todo";
                    await service.UpdateTaskAsync(task, cancellationToken);
                }

                if (settings.Json)
                {
#pragma warning disable IL2026
                    Console.WriteLine(System.Text.Json.JsonSerializer.Serialize(new { reset = true, count = allTasks.Count }, JsonHelper.Options));
#pragma warning restore IL2026
                }
                else
                {
                    Console.WriteLine($"Reset {allTasks.Count} task(s) to todo.");
                }
                return 0;
            }

            if (string.IsNullOrEmpty(settings.Id))
            {
                Console.Error.WriteLine("ERROR: ID is required. Use --all to reset all done tasks.");
                return 1;
            }

            var taskItem = await service.GetTaskByUidAsync(settings.Id, cancellationToken);

            if (taskItem == null)
            {
                Console.Error.WriteLine($"ERROR: Task with ID {settings.Id} not found.");
                return 1;
            }

            taskItem.Status = "todo";
            await service.UpdateTaskAsync(taskItem, cancellationToken);

            if (settings.Json)
            {
#pragma warning disable IL2026
                Console.WriteLine(System.Text.Json.JsonSerializer.Serialize(new { reset = true, id = settings.Id }, JsonHelper.Options));
#pragma warning restore IL2026
            }
            else
            {
                Console.WriteLine($"Task {settings.Id} reset to todo.");
            }

            return 0;
        }
    }
}
