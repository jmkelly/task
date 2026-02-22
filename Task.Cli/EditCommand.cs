using Spectre.Console.Cli;
using System.ComponentModel;
using System.Threading;

namespace Task.Cli
{
    [Description("Edit an existing task's properties. Specify the task ID and any fields to update. Use --json for LLM-friendly output.")]
    public class EditCommand : AsyncCommand<EditCommand.Settings>
    {
        public class Settings : Program.TaskCommandSettings
        {
            [CommandArgument(0, "<ids>")]
            [Description("The unique ID(s) of the task(s) to edit (e.g., 'a2b3k9' or 'a2b3k9 c4d5e6')")]
            public string? Ids { get; set; }

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

            [CommandOption("--project")]
            [Description("Update the project name (e.g., 'work', 'home'). Use empty string to remove project.")]
            public string? Project { get; set; }

            [CommandOption("--depends-on")]
            [Description("Update comma-separated task UIDs this task depends on (e.g., 'a1b2c3,d4e5f6'). Use empty string to clear dependencies.")]
            public string? DependsOn { get; set; }

            [CommandOption("-s|--status")]
            [Description("Update the status: todo, done, or in_progress (e.g., 'done')")]
            public string? Status { get; set; }
        }

        public override async Task<int> ExecuteAsync(CommandContext context, Settings settings, CancellationToken cancellationToken)
        {
            var service = await Program.GetTaskServiceAsync(settings, cancellationToken);

            if (string.IsNullOrEmpty(settings.Ids))
            {
                ErrorHelper.ShowError(
                    "ID is required.",
                    "task edit <id> --title 'New title'",
                    "task edit --help");
                return 1;
            }

            var ids = settings.Ids.Split(' ', StringSplitOptions.RemoveEmptyEntries);
            var updated = new List<string>();
            var failed = new List<string>();

            foreach (var id in ids)
            {
                var task = await service.GetTaskByUidAsync(id, cancellationToken);

                if (task == null)
                {
                    failed.Add(id);
                    continue;
                }

                if (!string.IsNullOrEmpty(settings.Title)) task.Title = settings.Title;
                if (!string.IsNullOrEmpty(settings.Description)) task.Description = settings.Description;
                if (!string.IsNullOrEmpty(settings.Priority))
                {
                    if (!ErrorHelper.ValidatePriority(settings.Priority, out var priorityError))
                    {
                        ErrorHelper.ShowError(priorityError!);
                        continue;
                    }
                    task.Priority = settings.Priority;
                }
                if (!string.IsNullOrEmpty(settings.DueDate))
                {
                    if (DateTime.TryParse(settings.DueDate, out var parsedDate))
                    {
                        task.DueDate = parsedDate;
                    }
                    else
                    {
                        ErrorHelper.ShowError(
                            $"Invalid date format for task {id}. Use YYYY-MM-DD.",
                            "task edit <id> --due-date 2024-02-20",
                            "task edit --help");
                        continue;
                    }
                }
                if (!string.IsNullOrEmpty(settings.Tags))
                {
                    task.Tags = settings.Tags.Split(',').Select(t => t.Trim()).ToList();
                }
                if (settings.Project != null)
                {
                    task.Project = string.IsNullOrEmpty(settings.Project) ? null : settings.Project;
                }
                if (settings.DependsOn != null)
                {
                    if (string.IsNullOrEmpty(settings.DependsOn))
                    {
                        task.DependsOn = new List<string>();
                    }
                    else
                    {
                        var newDeps = settings.DependsOn.Split(',').Select(t => t.Trim()).Where(t => !string.IsNullOrEmpty(t)).ToList();
                        var isValid = await service.ValidateDependenciesAsync(task.Uid, newDeps, cancellationToken);
                        if (!isValid)
                        {
                            ErrorHelper.ShowError($"Invalid dependencies for task {id}. Cannot create circular dependency or self-reference.");
                            continue;
                        }
                        
                        foreach (var depUid in newDeps)
                        {
                            var depTask = await service.GetTaskByUidAsync(depUid, cancellationToken);
                            if (depTask == null)
                            {
                                ErrorHelper.ShowError($"Task with UID '{depUid}' does not exist.");
                                continue;
                            }
                        }
                        task.DependsOn = newDeps;
                    }
                }
                if (!string.IsNullOrEmpty(settings.Status))
                {
                    if (!ErrorHelper.ValidateStatus(settings.Status, out var statusError))
                    {
                        ErrorHelper.ShowError($"Invalid status '{settings.Status}' for task {id}. {statusError}");
                        continue;
                    }
                    task.Status = settings.Status.ToLower();
                }

                await service.UpdateTaskAsync(task, cancellationToken);
                updated.Add(id);
            }

            if (settings.Json)
            {
                var results = new List<object>();
                foreach (var id in updated)
                {
                    results.Add(new { id, updated = true });
                }
                foreach (var id in failed)
                {
                    results.Add(new { id, updated = false, error = "not found" });
                }
#pragma warning disable IL2026
                Console.WriteLine(System.Text.Json.JsonSerializer.Serialize(results, JsonHelper.Options));
#pragma warning restore IL2026
            }
            else
            {
                if (updated.Count > 0)
                {
                    Console.WriteLine($"Task(s) {string.Join(", ", updated)} updated.");
                }
                if (failed.Count > 0)
                {
                    ErrorHelper.ShowError($"Task(s) not found: {string.Join(", ", failed)}");
                }
            }

            return failed.Count > 0 && updated.Count == 0 ? 1 : 0;
        }
    }
}