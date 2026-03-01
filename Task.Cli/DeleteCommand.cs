using Spectre.Console.Cli;
using System.ComponentModel;
using System.Threading;

namespace Task.Cli
{
    [Description("Delete a task permanently. Use --json for structured confirmation output.")]
    public class DeleteCommand : AsyncCommand<DeleteCommand.Settings>
    {
        public class Settings : Program.TaskCommandSettings
        {
            [CommandArgument(0, "[ids]")]
            [Description("The 6-character alpha UID(s) of the task(s) to delete (e.g., 'a2b3k9' or 'a2b3k9 c4d5e6')")]
            public string? Ids { get; set; }
        }

        public override async Task<int> ExecuteAsync(CommandContext context, Settings settings, CancellationToken cancellationToken)
        {
            var service = await Program.GetTaskServiceAsync(settings, cancellationToken);

            if (string.IsNullOrEmpty(settings.Ids))
            {
                ErrorHelper.ShowError(
                    "Task UID is required. (Provide at least one 6-character alpha UID, e.g., a2b3k9)",
                    "task delete <uid>",
                    "task delete --help");
                return 1;
            }

            var ids = settings.Ids.Split(' ', StringSplitOptions.RemoveEmptyEntries);
            var deleted = new List<string>();
            var failed = new List<string>();

            foreach (var id in ids)
            {
                var task = await service.GetTaskByUidAsync(id, cancellationToken);

                if (task == null)
                {
                    failed.Add(id);
                    continue;
                }

                await service.DeleteTaskAsync(id, cancellationToken);
                deleted.Add(id);
            }

            if (settings.Json)
            {
                var results = new List<object>();
                foreach (var uid in deleted)
                {
                    results.Add(new { uid, deleted = true });
                }
                foreach (var id in failed)
                {
                    results.Add(new { uid = id, deleted = false, error = "not found" });
                }
#pragma warning disable IL2026
                Console.WriteLine(System.Text.Json.JsonSerializer.Serialize(results, JsonHelper.Options));
#pragma warning restore IL2026
            }
            else
            {
                if (deleted.Count > 0)
                {
                    Console.WriteLine($"Task(s) {string.Join(", ", deleted)} archived.");
                }
                if (failed.Count > 0)
                {
                    ErrorHelper.ShowError($"Task UID(s) not found: {string.Join(", ", failed)}");
                }
            }

            return failed.Count > 0 && deleted.Count == 0 ? 1 : 0;
        }
    }
}