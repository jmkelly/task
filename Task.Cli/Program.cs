using Spectre.Console.Cli;
using System;
using System.IO;
using System.ComponentModel;
using System.Diagnostics.CodeAnalysis;
using Task.Core;

namespace Task.Cli
{
    public static class Program
    {
        [DynamicDependency(DynamicallyAccessedMemberTypes.All, typeof(AddCommand))]
        [DynamicDependency(DynamicallyAccessedMemberTypes.All, typeof(ListCommand))]
        [DynamicDependency(DynamicallyAccessedMemberTypes.All, typeof(EditCommand))]
        [DynamicDependency(DynamicallyAccessedMemberTypes.All, typeof(DeleteCommand))]
        [DynamicDependency(DynamicallyAccessedMemberTypes.All, typeof(CompleteCommand))]
        [DynamicDependency(DynamicallyAccessedMemberTypes.All, typeof(ResetCommand))]
        [DynamicDependency(DynamicallyAccessedMemberTypes.All, typeof(SearchCommand))]
        [DynamicDependency(DynamicallyAccessedMemberTypes.All, typeof(ImportCommand))]
        [DynamicDependency(DynamicallyAccessedMemberTypes.All, typeof(ConfigSetCommand))]
        [DynamicDependency(DynamicallyAccessedMemberTypes.All, typeof(ConfigGetCommand))]
        [DynamicDependency(DynamicallyAccessedMemberTypes.All, typeof(ConfigUnsetCommand))]
        [DynamicDependency(DynamicallyAccessedMemberTypes.All, typeof(ConfigListCommand))]
        [DynamicDependency(DynamicallyAccessedMemberTypes.All, typeof(HelpCommand))]
        public static int Main(string[] args)
        {
            var app = new CommandApp();

            app.Configure(config =>
            {
                config.SetApplicationName("task");
                config.SetApplicationVersion("1.0.0");

                // Commands
                config.AddCommand<AddCommand>("add")
                    .WithDescription("Create a new task with optional properties like priority, due date, tags, and project assignment")
                    .WithExample(new[] { "add", "Buy groceries" })
                    .WithExample(new[] { "add", "--title", "Refactor code", "--priority", "high", "--tags", "code,refactor" })
                    .WithExample(new[] { "add", "--title", "Upload report", "--due-date", "2024-04-01" })
                    .WithExample(new[] { "add", "--title", "Team meeting", "--project", "work", "--status", "in_progress" })
                    .WithExample(new[] { "add", "Buy groceries", "--priority", "high", "--assignee", "john.doe", "--tags", "errands", "--due-date", "2026-03-01" })
                    .WithExample(new[] { "add" });

                config.AddCommand<ListCommand>("list")
                    .WithDescription("Display tasks with advanced filtering by status, priority, assignee, project, tags, and due date")
                    .WithExample(new[] { "list" })
                    .WithExample(new[] { "list", "--status", "todo", "--assignee", "john.doe", "--json", "--sort", "due" });

                config.AddCommand<EditCommand>("edit")
                    .WithDescription("Modify existing task properties including title, description, priority, due date, and assignee")
                    .WithExample(new[] { "edit", "abc123", "--title", "New title" })
                    .WithExample(new[] { "edit", "abc123", "--priority", "high" })
                    .WithExample(new[] { "edit", "321", "--assignee", "jane.smith", "--priority", "low" });

                config.AddCommand<DeleteCommand>("delete")
                    .WithDescription("Permanently remove one or more tasks (supports bulk deletion with confirmation)")
                    .WithExample(new[] { "delete", "abc123" })
                    .WithExample(new[] { "delete", "abc123", "def456" });

                config.AddCommand<CompleteCommand>("complete")
                    .WithDescription("Mark tasks as completed (supports bulk completion)")
                    .WithExample(new[] { "complete", "abc123" })
                    .WithExample(new[] { "complete", "--all" });

                config.AddCommand<ResetCommand>("reset")
                    .WithDescription("Reset a completed task back to pending status")
                    .WithExample(new[] { "reset", "abc123" });

                config.AddCommand<SearchCommand>("search")
                    .WithDescription("Perform full-text or semantic similarity search across task titles and descriptions")
                    .WithExample(new[] { "search", "groceries" })
                    .WithExample(new[] { "search", "urgent", "--type", "hybrid" });

                config.AddCommand<ImportCommand>("import")
                    .WithDescription("Import tasks from JSON or CSV files, merging with existing data")
                    .WithExample(new[] { "import", "tasks.json" });

                config.AddBranch("config", branch =>
                {
                    branch.SetDescription("Manage CLI configuration settings");
                    branch.AddCommand<ConfigSetCommand>("set")
                        .WithDescription("Set a configuration value");
                    branch.AddCommand<ConfigGetCommand>("get")
                        .WithDescription("Retrieve a configuration value");
                    branch.AddCommand<ConfigUnsetCommand>("unset")
                        .WithDescription("Remove a configuration setting");
                    branch.AddCommand<ConfigListCommand>("list")
                        .WithDescription("Display all current configuration settings");
                });

                config.AddCommand<HelpCommand>("help")
                    .WithDescription("Show detailed help information");
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


            [CommandOption("--api-url")]
[Description("Base URL of the Task API (required)")]
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
            if (string.IsNullOrEmpty(settings.ApiUrl))
            {
                throw new InvalidOperationException("API URL must be specified. Please set it in your config (task config set api-url <URL>) or provide --api-url <URL> on the command line.");
            }
            await Config.ValidateUrlAsync(settings.ApiUrl);
            var apiClient = new ApiClient(settings.ApiUrl);
            await apiClient.InitializeAsync(cancellationToken);
            return apiClient;
        }
    }
}
