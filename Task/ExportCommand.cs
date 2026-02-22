using Spectre.Console;
using Spectre.Console.Cli;
using System.ComponentModel;
using System.Threading;
using System.Text.Json;
using System.IO;

namespace TaskApp
{
    [Description("Export tasks to JSON or CSV format. Outputs to file if specified, otherwise to stdout.")]
    public class ExportCommand : AsyncCommand<ExportCommand.Settings>
    {
        public class Settings : Program.TaskCommandSettings
        {
            [CommandOption("-f|--format")]
            [Description("Export format: json or csv (default: json)")]
            public string Format { get; set; } = "json";

            [CommandOption("-o|--output")]
            [Description("Output file path (optional, defaults to stdout)")]
            public string? Output { get; set; }
        }

        public override async Task<int> ExecuteAsync(CommandContext context, Settings settings, CancellationToken cancellationToken)
        {
            var service = await Program.GetTaskServiceAsync(settings, cancellationToken);

            try
            {
                var tasks = await service.GetAllTasksAsync(cancellationToken: cancellationToken);

                if (tasks.Count == 0)
                {
                    AnsiConsole.MarkupLine("[yellow]No tasks found to export.[/]");
                    return 0;
                }

                string output;
                if (string.Equals(settings.Format, "csv", StringComparison.OrdinalIgnoreCase))
                {
                    output = ExportToCsv(tasks);
                }
                else if (string.Equals(settings.Format, "json", StringComparison.OrdinalIgnoreCase))
                {
                    output = ExportToJson(tasks);
                }
                else
                {
                    Console.Error.WriteLine($"ERROR: Unsupported format '{settings.Format}'. Supported formats: json, csv.");
                    return 1;
                }

                if (!string.IsNullOrEmpty(settings.Output))
                {
                    // Write to file
                    AnsiConsole.Progress()
                        .Start(ctx =>
                        {
                            var task = ctx.AddTask("Writing to file", maxValue: 1);
                            File.WriteAllText(settings.Output, output);
                            task.Increment(1);
                            task.StopTask();
                        });
                    AnsiConsole.MarkupLine($"[green]Exported {tasks.Count} tasks to {settings.Output}[/]");
                }
                else
                {
                    // Output to stdout
                    Console.Write(output);
                }

                return 0;
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine($"ERROR: Export failed - {ex.Message}");
                return 1;
            }
        }

        private string ExportToJson(List<TaskItem> tasks)
        {
            var options = new JsonSerializerOptions
            {
                WriteIndented = true
            };

            if (tasks.Count > 500) // Only show progress for very large exports
            {
                string result = "";
                    AnsiConsole.Progress()
                        .Start(ctx =>
                        {
                            var task = ctx.AddTask("Serializing tasks to JSON", maxValue: 1);
#pragma warning disable IL2026
                            result = JsonSerializer.Serialize(tasks, JsonHelper.Options);
#pragma warning restore IL2026
                            task.Increment(1);
                            task.StopTask();
                        });
                return result;
            }
            else
            {
#pragma warning disable IL2026
                return JsonSerializer.Serialize(tasks, JsonHelper.Options);
#pragma warning restore IL2026
            }
        }

        private string ExportToCsv(List<TaskItem> tasks)
        {
            var csv = new System.Text.StringBuilder();
            // Header
            csv.AppendLine("Uid,Title,Description,Priority,DueDate,Tags,Status,CreatedAt,UpdatedAt");

            if (tasks.Count > 100) // Only show progress for large exports
            {
                string result = "";
                AnsiConsole.Progress()
                    .Start(ctx =>
                    {
                        var task = ctx.AddTask("Processing tasks for CSV export", maxValue: tasks.Count);
                        foreach (var taskItem in tasks)
                        {
                            var line = $"{EscapeCsv(taskItem.Uid)},{EscapeCsv(taskItem.Title)},{EscapeCsv(taskItem.Description ?? "")},{taskItem.Priority},{taskItem.DueDateString},{EscapeCsv(taskItem.TagsString)},{taskItem.Status},{taskItem.CreatedAt:o},{taskItem.UpdatedAt:o}";
                            csv.AppendLine(line);
                            task.Increment(1);
                        }
                        task.StopTask();
                        result = csv.ToString();
                    });
                return result;
            }
            else
            {
                foreach (var task in tasks)
                {
                    var line = $"{EscapeCsv(task.Uid)},{EscapeCsv(task.Title)},{EscapeCsv(task.Description ?? "")},{task.Priority},{task.DueDateString},{EscapeCsv(task.TagsString)},{task.Status},{task.CreatedAt:o},{task.UpdatedAt:o}";
                    csv.AppendLine(line);
                }
                return csv.ToString();
            }
        }

        private string EscapeCsv(string value)
        {
            if (value.Contains(",") || value.Contains("\"") || value.Contains("\n"))
            {
                return "\"" + value.Replace("\"", "\"\"") + "\"";
            }
            return value;
        }
    }
}