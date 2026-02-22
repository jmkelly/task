using Spectre.Console;
using Spectre.Console.Cli;
using System.ComponentModel;
using System.Threading;

namespace Task.Cli
{
    [Description("Add a new task to the task list. Use --json for LLM-friendly structured output.")]
    public class AddCommand : AsyncCommand<AddCommand.Settings>
    {
        public class Settings : Program.TaskCommandSettings
        {
            [CommandArgument(0, "[title]")]
            [Description("The task title. Use quotes for multi-word titles (e.g., 'Buy groceries'). " +
                        "Cannot be used together with --title.")]
            public string? Title { get; set; }

            [CommandOption("-t|--title")]
            [Description("The task title as an option (alternative to positional argument). " +
                        "Use when title starts with a dash or for consistency with other commands.")]
            public string? TitleOption { get; set; }

            [CommandOption("-d|--description")]
            [Description("A detailed description of the task. " +
                        "Example: -d 'Buy milk, bread, and eggs from the store'")]
            public string? Description { get; set; }

            [CommandOption("-p|--priority")]
            [Description("Priority level for the task. Valid values: low, medium, high. " +
                        "Default: medium. Example: --priority high")]
            public string Priority { get; set; } = "medium";

            [CommandOption("--due-date")]
            [Description("Due date for the task in YYYY-MM-DD format. " +
                        "Example: --due-date 2024-04-01")]
            public string? DueDate { get; set; }

            [CommandOption("--tags")]
            [Description("Comma-separated list of tags for organization. " +
                        "Example: --tags shopping,urgent,weekly")]
            public string? Tags { get; set; }

            [CommandOption("--project")]
            [Description("Project name to group related tasks. " +
                        "Example: --project work or --project home")]
            public string? Project { get; set; }

            [CommandOption("--depends-on")]
            [Description("Comma-separated list of task UIDs that this task depends on. " +
                        "Example: --depends-on a1b2c3,d4e5f6")]
            public string? DependsOn { get; set; }

            [CommandOption("-s|--status")]
            [Description("Initial status of the task. Valid values: todo, done, in_progress. " +
                        "Default: todo. Example: --status in_progress")]
            public string? Status { get; set; }
        }

        public override async Task<int> ExecuteAsync(CommandContext context, Settings settings, CancellationToken cancellationToken)
        {
            var service = await Program.GetTaskServiceAsync(settings, cancellationToken);

            string? title = null;
            string? description = settings.Description;
            string priority = settings.Priority;
            DateTime? dueDate = null;
            var tags = string.IsNullOrEmpty(settings.Tags) ? new List<string>() : settings.Tags.Split(',').Select(t => t.Trim()).ToList();
            string? project = settings.Project;
            var dependsOn = string.IsNullOrEmpty(settings.DependsOn) ? new List<string>() : settings.DependsOn.Split(',').Select(t => t.Trim()).ToList();

            // Handle title: either positional or --title option, but not both
            if (!string.IsNullOrEmpty(settings.Title) && !string.IsNullOrEmpty(settings.TitleOption))
            {
                ErrorHelper.ShowError(
                    "Use only one of positional title or --title option.",
                    "task add 'Task title' OR task add --title 'Task title'",
                    "task add --help");
                return 1;
            }

            title = !string.IsNullOrEmpty(settings.Title) ? settings.Title : settings.TitleOption;

            // Validate priority
            if (!ErrorHelper.ValidatePriority(priority, out var priorityError))
            {
                ErrorHelper.ShowError(priorityError!);
                return 1;
            }

            // Validate status
            if (!ErrorHelper.ValidateStatus(settings.Status, out var statusError))
            {
                ErrorHelper.ShowError(statusError!);
                return 1;
            }

            // Validate due date
            if (!ErrorHelper.ValidateDate(settings.DueDate, out var dateError))
            {
                ErrorHelper.ShowError(dateError!);
                return 1;
            }

            // If no title provided, run in interactive mode
            if (string.IsNullOrEmpty(title))
            {
                // Interactive mode: prompt for inputs
                title = AnsiConsole.Prompt(
                    new TextPrompt<string>("Task title:")
                        .PromptStyle("yellow")
                        .Validate(t => !string.IsNullOrWhiteSpace(t), "Title cannot be empty."));

                description = AnsiConsole.Prompt(
                    new TextPrompt<string?>("Task description (optional):")
                        .AllowEmpty());

                priority = AnsiConsole.Prompt(
                    new SelectionPrompt<string>()
                        .Title("Priority:")
                        .AddChoices("high", "medium", "low")
                        .UseConverter(p => p switch
                        {
                            "high" => "[red]high[/]",
                            "medium" => "[yellow]medium[/]",
                            "low" => "[green]low[/]",
                            _ => p
                        }));

                var dueDateInput = AnsiConsole.Prompt(
                    new TextPrompt<string?>("Due date (YYYY-MM-DD, optional):")
                        .AllowEmpty()
                        .Validate(date =>
                        {
                            if (string.IsNullOrEmpty(date)) return ValidationResult.Success();
                            return DateTime.TryParse(date, out _) ? ValidationResult.Success() : ValidationResult.Error("Invalid date format. Use YYYY-MM-DD.");
                        }));

                if (!string.IsNullOrEmpty(dueDateInput))
                {
                    dueDate = DateTime.Parse(dueDateInput);
                }

                var availableTags = await service.GetAllUniqueTagsAsync(cancellationToken);
                if (availableTags.Count > 0)
                {
                    var selectedTags = AnsiConsole.Prompt(
                        new MultiSelectionPrompt<string>()
                            .Title("Tags (use space to select, enter to confirm):")
                            .NotRequired()
                            .AddChoices(availableTags));

                    tags = selectedTags.ToList();
                }
                else
                {
                    var tagsInput = AnsiConsole.Prompt(
                        new TextPrompt<string?>("Tags (comma-separated, optional):")
                            .AllowEmpty());
                    
                    if (!string.IsNullOrEmpty(tagsInput))
                    {
                        tags = tagsInput.Split(',').Select(t => t.Trim()).Where(t => !string.IsNullOrEmpty(t)).ToList();
                    }
                }
            }
            else
            {
                // Non-interactive mode validation
                if (!string.IsNullOrEmpty(settings.DueDate))
                {
                    if (DateTime.TryParse(settings.DueDate, out var parsedDate))
                    {
                        dueDate = parsedDate;
                    }
                }
            }

            var task = await service.AddTaskAsync(title, description, priority, dueDate, tags, project, dependsOn, settings.Status, cancellationToken);
            
            Console.Error.WriteLine($"DEBUG: project='{project}', settings.Project='{settings.Project}'");
            Console.Error.WriteLine($"DEBUG: task.Project='{task.Project}'");

            if (settings.Json)
            {
#pragma warning disable IL2026
                Console.WriteLine(System.Text.Json.JsonSerializer.Serialize(task, JsonHelper.Options));
#pragma warning restore IL2026
            }
            else
            {
                Console.WriteLine($"Task added: {task.Id}");
            }

            return 0;
        }
    }
}
