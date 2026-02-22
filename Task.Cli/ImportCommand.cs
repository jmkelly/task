using Spectre.Console;
using Spectre.Console.Cli;
using System.ComponentModel;
using System.Threading;
using System.Text.Json;
using System.IO;
using Task.Core;
using System.Threading.Tasks;

namespace Task.Cli
{
    [Description("Import tasks from JSON or CSV format.")]
    public class ImportCommand : AsyncCommand<ImportCommand.Settings>
    {
        public class Settings : Program.TaskCommandSettings
        {
            [CommandArgument(0, "[input]")]
            [Description("Input file path")]
            public string? Input { get; set; }

            [CommandOption("-f|--format")]
            [Description("Import format: json or csv (default: json)")]
            public string Format { get; set; } = "json";
        }

        public override async Task<int> ExecuteAsync(CommandContext context, Settings settings, CancellationToken cancellationToken)
        {
            var service = await Program.GetTaskServiceAsync(settings, cancellationToken);

            if (string.IsNullOrEmpty(settings.Input))
            {
                Console.Error.WriteLine("ERROR: Input file is required.");
                return 1;
            }

            if (!File.Exists(settings.Input))
            {
                Console.Error.WriteLine($"ERROR: Input file '{settings.Input}' does not exist.");
                return 1;
            }

            try
            {
                List<TaskItem> tasks;
                if (string.Equals(settings.Format, "csv", StringComparison.OrdinalIgnoreCase))
                {
                    tasks = ImportFromCsv(settings.Input);
                }
                else if (string.Equals(settings.Format, "json", StringComparison.OrdinalIgnoreCase))
                {
                    tasks = ImportFromJson(settings.Input);
                }
                else
                {
                    Console.Error.WriteLine($"ERROR: Unsupported format '{settings.Format}'. Supported formats: json, csv.");
                    return 1;
                }

                var addTasks = new List<System.Threading.Tasks.Task>();
                int imported = 0;
                AnsiConsole.Progress()
                    .Start(ctx =>
                    {
                        var task = ctx.AddTask($"Importing {tasks.Count} tasks", maxValue: tasks.Count);
                        foreach (var taskItem in tasks)
                        {
                            try
                            {
                                // Validate required fields
                                if (string.IsNullOrWhiteSpace(taskItem.Title))
                                {
                                    AnsiConsole.MarkupLine($"[red]Skipping task with empty title.[/]");
                                    continue;
                                }

                                // Set defaults if missing
                                taskItem.Priority = string.IsNullOrEmpty(taskItem.Priority) ? "medium" : taskItem.Priority;
                                taskItem.Status = string.IsNullOrEmpty(taskItem.Status) ? "pending" : taskItem.Status;

                                // Add to database (this will generate new UID and timestamps)
                                addTasks.Add(service.AddTaskAsync(taskItem.Title, taskItem.Description, taskItem.Priority, taskItem.DueDate, taskItem.Tags, taskItem.Project, taskItem.DependsOn, taskItem.Assignee, taskItem.Status, cancellationToken));
                                imported++;
                                task.Increment(1);
                            }
                            catch (Exception ex)
                            {
                                AnsiConsole.MarkupLine($"[red]Failed to import task '{taskItem.Title}': {ex.Message}[/]");
                            }
                        }
                        task.StopTask();
                    });

                await System.Threading.Tasks.Task.WhenAll(addTasks);

                AnsiConsole.MarkupLine($"[green]Successfully imported {imported} out of {tasks.Count} tasks.[/]");
                return 0;
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine($"ERROR: Import failed - {ex.Message}");
                return 1;
            }
        }

        private List<TaskItem> ImportFromJson(string filePath)
        {
            List<TaskItem>? tasks = null;
            AnsiConsole.Progress()
                .Start(async ctx =>
                {
                    var task = ctx.AddTask("Reading JSON file", maxValue: 1);
                    var json = File.ReadAllText(filePath);
                    task.Increment(0.5);
#pragma warning disable IL2026
                    tasks = JsonSerializer.Deserialize<List<TaskItem>>(json, JsonHelper.Options) ?? new List<TaskItem>();
#pragma warning restore IL2026
                    task.Increment(0.5);
                    task.StopTask();
                });
            return tasks ?? new List<TaskItem>();
        }

        private List<TaskItem> ImportFromCsv(string filePath)
        {
            var tasks = new List<TaskItem>();
            var lines = File.ReadAllLines(filePath);

            if (lines.Length < 2)
            {
                throw new Exception("CSV file must have at least a header row and one data row.");
            }

            var headers = ParseCsvLine(lines[0]);
            var headerMap = new Dictionary<string, int>();
            for (int i = 0; i < headers.Length; i++)
            {
                headerMap[headers[i].ToLower()] = i;
            }

            // Required columns
            if (!headerMap.ContainsKey("title"))
            {
                throw new Exception("CSV must contain a 'Title' column.");
            }

            if (lines.Length > 100) // Show progress for large files
            {
                AnsiConsole.Progress()
                    .Start(ctx =>
                    {
                        var task = ctx.AddTask("Parsing CSV data", maxValue: lines.Length - 1);
                        for (int i = 1; i < lines.Length; i++)
                        {
                            var values = ParseCsvLine(lines[i]);
                            if (values.Length != headers.Length)
                            {
                                throw new Exception($"Row {i + 1} has {values.Length} columns, expected {headers.Length}.");
                            }

                            var taskItem = new TaskItem
                            {
                                Id = 0,
                                Uid = "",
                                Title = values[headerMap["title"]],
                                Description = headerMap.ContainsKey("description") ? values[headerMap["description"]] : null,
                                Priority = headerMap.ContainsKey("priority") ? values[headerMap["priority"]] : "medium",
                                DueDate = headerMap.ContainsKey("duedate") && !string.IsNullOrEmpty(values[headerMap["duedate"]]) ?
                                    DateTime.TryParse(values[headerMap["duedate"]], out var dd) ? dd : null : null,
                                Tags = headerMap.ContainsKey("tags") && !string.IsNullOrEmpty(values[headerMap["tags"]]) ?
                                    values[headerMap["tags"]].Split(',').Select(t => t.Trim()).Where(t => !string.IsNullOrEmpty(t)).ToList() : new List<string>(),
                                Status = headerMap.ContainsKey("status") ? values[headerMap["status"]] : "pending",
                                CreatedAt = DateTime.UtcNow,
                                UpdatedAt = DateTime.UtcNow
                            };

                            tasks.Add(taskItem);
                            task.Increment(1);
                        }
                        task.StopTask();
                    });
            }
            else
            {
                for (int i = 1; i < lines.Length; i++)
                {
                    var values = ParseCsvLine(lines[i]);
                    if (values.Length != headers.Length)
                    {
                        throw new Exception($"Row {i + 1} has {values.Length} columns, expected {headers.Length}.");
                    }

                    var task = new TaskItem
                    {
                        Id = 0,
                        Uid = "",
                        Title = values[headerMap["title"]],
                        Description = headerMap.ContainsKey("description") ? values[headerMap["description"]] : null,
                        Priority = headerMap.ContainsKey("priority") ? values[headerMap["priority"]] : "medium",
                        DueDate = headerMap.ContainsKey("duedate") && !string.IsNullOrEmpty(values[headerMap["duedate"]]) ?
                            DateTime.TryParse(values[headerMap["duedate"]], out var dd) ? dd : null : null,
                        Tags = headerMap.ContainsKey("tags") && !string.IsNullOrEmpty(values[headerMap["tags"]]) ?
                            values[headerMap["tags"]].Split(',').Select(t => t.Trim()).Where(t => !string.IsNullOrEmpty(t)).ToList() : new List<string>(),
                        Status = headerMap.ContainsKey("status") ? values[headerMap["status"]] : "pending",
                        CreatedAt = DateTime.UtcNow,
                        UpdatedAt = DateTime.UtcNow
                    };

                    tasks.Add(task);
                }
            }

            return tasks;
        }

        private string[] ParseCsvLine(string line)
        {
            var result = new List<string>();
            bool inQuotes = false;
            var current = new System.Text.StringBuilder();

            for (int i = 0; i < line.Length; i++)
            {
                char c = line[i];
                if (c == '"')
                {
                    if (inQuotes && i + 1 < line.Length && line[i + 1] == '"')
                    {
                        current.Append('"');
                        i++; // Skip next quote
                    }
                    else
                    {
                        inQuotes = !inQuotes;
                    }
                }
                else if (c == ',' && !inQuotes)
                {
                    result.Add(current.ToString());
                    current.Clear();
                }
                else
                {
                    current.Append(c);
                }
            }

            result.Add(current.ToString());
            return result.ToArray();
        }
    }
}