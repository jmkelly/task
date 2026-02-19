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
        [DynamicDependency(DynamicallyAccessedMemberTypes.All, typeof(SearchCommand))]
        [DynamicDependency(DynamicallyAccessedMemberTypes.All, typeof(ExportCommand))]
        [DynamicDependency(DynamicallyAccessedMemberTypes.All, typeof(ImportCommand))]
        public static int Main(string[] args)
        {
            var app = new CommandApp();

            app.Configure(config =>
            {
                config.SetApplicationName("task");
                config.SetApplicationVersion("1.0.0");

                // Commands
                config.AddCommand<AddCommand>("add");
                config.AddCommand<ListCommand>("list");
                config.AddCommand<EditCommand>("edit");
                config.AddCommand<DeleteCommand>("delete");
                config.AddCommand<CompleteCommand>("complete");
                config.AddCommand<SearchCommand>("search");
                config.AddCommand<ExportCommand>("export");
                config.AddCommand<ImportCommand>("import");
            });

            return app.Run(args);
        }

        // Shared settings for commands
        public class TaskCommandSettings : CommandSettings
        {
            [CommandOption("--json")]
            [Description("Output results in JSON format for machine parsing and LLM integration")]
            public bool Json { get; set; }

            [CommandOption("--plain")]
            [Description("Output in plain text format, disabling rich formatting")]
            public bool Plain { get; set; }

            [CommandOption("--db")]
            [Description("Path to the database file (default: tasks.db)")]
            public string DatabasePath { get; set; } = "tasks.db";
        }

        public static Database GetDatabase(TaskCommandSettings settings)
        {
            var dbPath = Path.GetFullPath(settings.DatabasePath);
            return new Database(dbPath);
        }
    }
}
