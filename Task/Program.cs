using Spectre.Console.Cli;
using System;
using System.IO;
using System.ComponentModel;
using System.Diagnostics.CodeAnalysis;

namespace TaskApp
{
    public static class Program
    {
        [DynamicDependency(DynamicallyAccessedMemberTypes.All, typeof(AddCommand))]
        [DynamicDependency(DynamicallyAccessedMemberTypes.All, typeof(ListCommand))]
        [DynamicDependency(DynamicallyAccessedMemberTypes.All, typeof(EditCommand))]
        [DynamicDependency(DynamicallyAccessedMemberTypes.All, typeof(DeleteCommand))]
        [DynamicDependency(DynamicallyAccessedMemberTypes.All, typeof(CompleteCommand))]
        [DynamicDependency(DynamicallyAccessedMemberTypes.All, typeof(ResetCommand))]
        [DynamicDependency(DynamicallyAccessedMemberTypes.All, typeof(UndoCommand))]
        [DynamicDependency(DynamicallyAccessedMemberTypes.All, typeof(SearchCommand))]
        [DynamicDependency(DynamicallyAccessedMemberTypes.All, typeof(ExportCommand))]
        [DynamicDependency(DynamicallyAccessedMemberTypes.All, typeof(ImportCommand))]
        [DynamicDependency(DynamicallyAccessedMemberTypes.All, typeof(ConfigSetCommand))]
        [DynamicDependency(DynamicallyAccessedMemberTypes.All, typeof(ConfigGetCommand))]
        [DynamicDependency(DynamicallyAccessedMemberTypes.All, typeof(ConfigUnsetCommand))]
        [DynamicDependency(DynamicallyAccessedMemberTypes.All, typeof(ConfigListCommand))]
        public static int Main(string[] args)
        {
            var app = new CommandApp();

            app.Configure(config =>
            {
                config.SetApplicationName("task");
                config.SetApplicationVersion("1.0.0");

                // Commands
                config.AddCommand<AddCommand>("add")
                    .WithDescription("Add a new task to the task list. " +
                        "Supports quick add with just a title, or full options for detailed tasks. " +
                        "Use without arguments for interactive mode.")
                    .WithExample(new[] { "add", "\"Buy milk\"" })
                    .WithExample(new[] { "add", "--title", "\"Refactor code\"", "--priority", "high", "--tags", "code,refactor" })
                    .WithExample(new[] { "add", "--title", "\"Upload report\"", "--due-date", "2024-04-01" })
                    .WithExample(new[] { "add", "--title", "\"Team meeting\"", "--project", "work", "--status", "in_progress" })
                    .WithExample(new[] { "add" });

                config.AddCommand<ListCommand>("list")
                    .WithExample(new[] { "list" })
                    .WithExample(new[] { "list", "--status", "todo" });

                config.AddCommand<EditCommand>("edit")
                    .WithExample(new[] { "edit", "abc123", "--title", "\"New title\"" })
                    .WithExample(new[] { "edit", "abc123", "--priority", "high" });

                config.AddCommand<DeleteCommand>("delete")
                    .WithExample(new[] { "delete", "abc123" })
                    .WithExample(new[] { "delete", "abc123", "def456" });

                config.AddCommand<CompleteCommand>("complete")
                    .WithExample(new[] { "complete", "abc123" })
                    .WithExample(new[] { "complete", "--all" });

                config.AddCommand<ResetCommand>("reset")
                    .WithExample(new[] { "reset", "abc123" });

                config.AddCommand<UndoCommand>("undo")
                    .WithExample(new[] { "undo" });

                config.AddCommand<SearchCommand>("search")
                    .WithExample(new[] { "search", "groceries" })
                    .WithExample(new[] { "search", "urgent", "--type", "hybrid" });

                config.AddCommand<ExportCommand>("export")
                    .WithExample(new[] { "export" })
                    .WithExample(new[] { "export", "--format", "json" });

                config.AddCommand<ImportCommand>("import")
                    .WithExample(new[] { "import", "tasks.json" });

                config.AddBranch("config", branch =>
                {
                    branch.AddCommand<ConfigSetCommand>("set");
                    branch.AddCommand<ConfigGetCommand>("get");
                    branch.AddCommand<ConfigUnsetCommand>("unset");
                    branch.AddCommand<ConfigListCommand>("list");
                });
            });

            return app.Run(args);
        }

        // Shared settings for commands
        public class TaskCommandSettings : CommandSettings
        {
            private static readonly Config _config = Config.Load();

            [CommandOption("--json")]
            [Description("Output results in JSON format for machine parsing and LLM integration")]
            public bool Json { get; set; }

            [CommandOption("--plain")]
            [Description("Output in plain text format, disabling rich formatting")]
            public bool Plain { get; set; }

            [CommandOption("--db")]
            [Description("Path to the database file (default: tasks.db)")]
            public string DatabasePath { get; set; } = "tasks.db";

            [CommandOption("--api-url")]
            [Description("Base URL of the Task API (if not specified, uses local database)")]
            public string? ApiUrl { get; set; }

            public TaskCommandSettings()
            {
                // Load defaults from config
                if (_config.DefaultOutput == "json")
                {
                    Json = true;
                }
                else if (_config.DefaultOutput == "plain")
                {
                    Plain = true;
                }

                if (!string.IsNullOrEmpty(_config.ApiUrl))
                {
                    ApiUrl = _config.ApiUrl;
                }
            }
        }

        public static async System.Threading.Tasks.Task<ITaskService> GetTaskServiceAsync(TaskCommandSettings settings, CancellationToken cancellationToken = default)
        {
            var apiUrl = settings.ApiUrl ?? "http://localhost:5000";
            var apiClient = new ApiClient(apiUrl);
            await apiClient.InitializeAsync(cancellationToken);
            return apiClient;
        }
    }
}
