using Spectre.Console.Cli;
using System.ComponentModel;
using System.Threading;

namespace TaskApp
{
    [Description("Delete a task permanently. Use --json for structured confirmation output.")]
    public class DeleteCommand : AsyncCommand<DeleteCommand.Settings>
    {
        public class Settings : Program.TaskCommandSettings
        {
            [CommandArgument(0, "<id>")]
            [Description("The unique ID of the task to delete (e.g., 'a2b3k9')")]
            public string? Id { get; set; }
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

            await db.DeleteTask(task.Id.ToString(), cancellationToken);

            if (settings.Json)
            {
#pragma warning disable IL2026
                Console.WriteLine(System.Text.Json.JsonSerializer.Serialize(new { deleted = true, id = settings.Id }));
#pragma warning restore IL2026
            }
            else
            {
                Console.WriteLine($"Task {settings.Id} deleted.");
            }

            return 0;
        }
    }
}