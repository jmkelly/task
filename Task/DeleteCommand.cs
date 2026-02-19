using Spectre.Console.Cli;
using System.ComponentModel;
using System.Threading;

namespace TaskApp
{
    [Description("Delete a task permanently. Use --json for structured confirmation output.")]
    public class DeleteCommand : Command<DeleteCommand.Settings>
    {
        public class Settings : Program.TaskCommandSettings
        {
            [CommandArgument(0, "<id>")]
            [Description("The unique ID of the task to delete (e.g., 'a2b3k9')")]
            public string? Id { get; set; }
        }

        public override int Execute(CommandContext context, Settings settings, CancellationToken cancellationToken)
        {
            var db = Program.GetDatabase(settings);

            if (string.IsNullOrEmpty(settings.Id))
            {
                Console.Error.WriteLine("ERROR: ID is required.");
                return 1;
            }

            var task = db.GetTaskByUid(settings.Id);

            if (task == null)
            {
                Console.Error.WriteLine($"ERROR: Task with ID {settings.Id} not found.");
                return 1;
            }

            db.DeleteTask(task.Id.ToString());

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