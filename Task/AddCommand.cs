using Spectre.Console;
using Spectre.Console.Cli;
using System.ComponentModel;
using System.Threading;

namespace TaskApp
{
    [Description("Add a new task to the task list. Use --json for LLM-friendly structured output.")]
    public class AddCommand : AsyncCommand<AddCommand.Settings>
    {
        public class Settings : Program.TaskCommandSettings
        {
            [CommandOption("-t|--title")]
            [Description("The title of the task (optional for interactive mode)")]
            public string? Title { get; set; }

            [CommandOption("-d|--description")]
            [Description("A brief description of the task (e.g., 'Milk, bread, eggs')")]
            public string? Description { get; set; }

            [CommandOption("-p|--priority")]
            [Description("Priority level: high, medium, or low (default: medium)")]
            public string Priority { get; set; } = "medium";

            [CommandOption("--due-date")]
            [Description("Due date in YYYY-MM-DD format (e.g., '2024-02-20')")]
            public string? DueDate { get; set; }

            [CommandOption("--tags")]
            [Description("Comma-separated list of tags (e.g., 'shopping,urgent')")]
            public string? Tags { get; set; }
        }

        public override async Task<int> ExecuteAsync(CommandContext context, Settings settings, CancellationToken cancellationToken)
        {
            var db = await Program.GetDatabaseAsync(settings, cancellationToken);

            string? title = settings.Title;
            string? description = settings.Description;
            string priority = settings.Priority;
            DateTime? dueDate = null;
            var tags = string.IsNullOrEmpty(settings.Tags) ? new List<string>() : settings.Tags.Split(',').Select(t => t.Trim()).ToList();

            if (string.IsNullOrEmpty(settings.Title))
            {
                // Interactive mode: prompt for inputs
                if (string.IsNullOrEmpty(title))
                {
                    title = AnsiConsole.Prompt(
                        new TextPrompt<string>("Task title:")
                            .PromptStyle("yellow")
                            .Validate(t => !string.IsNullOrWhiteSpace(t), "Title cannot be empty."));
                }

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

                var availableTags = await db.GetAllUniqueTags(cancellationToken);
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
                if (string.IsNullOrEmpty(title))
                {
                    Console.Error.WriteLine("ERROR: Title is required when not in interactive mode.");
                    return 1;
                }

                if (!string.IsNullOrEmpty(settings.DueDate))
                {
                    if (DateTime.TryParse(settings.DueDate, out var parsedDate))
                    {
                        dueDate = parsedDate;
                    }
                    else
                    {
                        Console.Error.WriteLine("ERROR: Invalid date format. Use YYYY-MM-DD.");
                        return 1;
                    }
                }
            }

            var task = await db.AddTask(title, description, priority, dueDate, tags, cancellationToken);

            if (settings.Json)
            {
#pragma warning disable IL2026
                Console.WriteLine(System.Text.Json.JsonSerializer.Serialize(task));
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