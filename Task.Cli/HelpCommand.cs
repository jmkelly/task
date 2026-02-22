using Spectre.Console;
using Spectre.Console.Cli;
using System.ComponentModel;

namespace Task.Cli
{
	public class HelpCommand : AsyncCommand<HelpCommand.Settings>
	{
		public class Settings : CommandSettings
		{
		}

		public override async Task<int> ExecuteAsync(CommandContext context, Settings settings, CancellationToken cancellationToken)
		{
			// NAME
			AnsiConsole.MarkupLine("[yellow]NAME[/]");
			AnsiConsole.WriteLine("task - A powerful, interactive CLI tool for efficient task and todo management");
			AnsiConsole.WriteLine();

			// SYNOPSIS
			AnsiConsole.MarkupLine("[yellow]SYNOPSIS[/]");
			AnsiConsole.WriteLine("task [GLOBAL OPTIONS] <COMMAND> [COMMAND OPTIONS] [ARGS...]");
			AnsiConsole.WriteLine();

			// DESCRIPTION
			AnsiConsole.MarkupLine("[yellow]DESCRIPTION[/]");
			AnsiConsole.WriteLine("Task is a modern command-line interface designed for managing personal and team tasks with advanced features like priorities, due dates, projects, tags, dependencies, and semantic search. It supports both local SQLite database storage and remote API integration for collaborative workflows.");
			AnsiConsole.WriteLine();
			AnsiConsole.WriteLine("Key features include:");
			AnsiConsole.WriteLine("- Interactive and non-interactive task creation");
			AnsiConsole.WriteLine("- Flexible filtering and searching capabilities");
			AnsiConsole.WriteLine("- Priority and status management");
			AnsiConsole.WriteLine("- Project-based organization");
			AnsiConsole.WriteLine("- Dependency tracking");
			AnsiConsole.WriteLine("- JSON output for scripting and LLM integration");
			AnsiConsole.WriteLine("- Semantic search using vector embeddings");
			AnsiConsole.WriteLine("- Export/import functionality (JSON, CSV)");
			AnsiConsole.WriteLine("- Configuration management");
			AnsiConsole.WriteLine("- Bulk operations support");
			AnsiConsole.WriteLine();
			AnsiConsole.WriteLine("The tool prioritizes human-first design with rich, colored output while maintaining machine-readable interfaces for automation.");
			AnsiConsole.WriteLine();

			// GLOBAL OPTIONS
			AnsiConsole.MarkupLine("[yellow]GLOBAL OPTIONS[/]");
			AnsiConsole.WriteLine("These options apply to all commands:");
			AnsiConsole.WriteLine();
			AnsiConsole.WriteLine("-h, --help          Display this help message and exit");
			AnsiConsole.WriteLine("-v, --version       Display version information and exit");
			AnsiConsole.WriteLine("--json              Output results in JSON format for scripting and LLM integration");
			AnsiConsole.WriteLine("--plain             Output in plain text format, disabling rich formatting and colors");
			AnsiConsole.WriteLine("--db <PATH>         Path to the SQLite database file (default: tasks.db in current directory)");
			AnsiConsole.WriteLine("--api-url <URL>     Base URL of the Task API server (overrides local database usage)");
			AnsiConsole.WriteLine();

			// COMMANDS
			AnsiConsole.MarkupLine("[yellow]COMMANDS[/]");
			AnsiConsole.WriteLine();

			// Task Management
			AnsiConsole.MarkupLine("[yellow]Task Management[/]");
			AnsiConsole.WriteLine("add                 Create a new task with optional properties like priority, due date, tags, and project assignment");
			AnsiConsole.WriteLine("list                Display tasks with advanced filtering by status, priority, assignee, project, tags, and due date");
			AnsiConsole.WriteLine("edit <ID>           Modify existing task properties including title, description, priority, due date, and assignee");
			AnsiConsole.WriteLine("delete <ID...>      Permanently remove one or more tasks (supports bulk deletion with confirmation)");
			AnsiConsole.WriteLine("complete <ID...>    Mark tasks as completed (supports bulk completion)");
			AnsiConsole.WriteLine("reset <ID>          Reset a completed task back to pending status");
			AnsiConsole.WriteLine();

			// Search and Discovery
			AnsiConsole.MarkupLine("[yellow]Search and Discovery[/]");
			AnsiConsole.WriteLine("search <QUERY>      Perform full-text or semantic similarity search across task titles and descriptions");
			AnsiConsole.WriteLine();

			// Data Management
			AnsiConsole.MarkupLine("[yellow]Data Management[/]");
			AnsiConsole.WriteLine("export              Export all tasks to JSON or CSV format for backup or migration");
			AnsiConsole.WriteLine("import <FILE>       Import tasks from JSON or CSV files, merging with existing data");
			AnsiConsole.WriteLine();

			// Configuration
			AnsiConsole.MarkupLine("[yellow]Configuration[/]");
			AnsiConsole.WriteLine("config              Manage CLI configuration settings");
			AnsiConsole.WriteLine("  set <KEY> <VALUE> Set a configuration value");
			AnsiConsole.WriteLine("  get <KEY>         Retrieve a configuration value");
			AnsiConsole.WriteLine("  unset <KEY>       Remove a configuration setting");
			AnsiConsole.WriteLine("  list              Display all current configuration settings");
			AnsiConsole.WriteLine();

			// EXAMPLES
			AnsiConsole.MarkupLine("[yellow]EXAMPLES[/]");
			AnsiConsole.WriteLine();

			// Basic Task Operations
			AnsiConsole.MarkupLine("[yellow]Basic Task Operations[/]");
			AnsiConsole.WriteLine("```bash");
			AnsiConsole.WriteLine("# Add a simple task interactively");
			AnsiConsole.WriteLine("task add");
			AnsiConsole.WriteLine();
			AnsiConsole.WriteLine("# Add task with title directly");
			AnsiConsole.WriteLine("task add \"Review pull request #123\"");
			AnsiConsole.WriteLine();
			AnsiConsole.WriteLine("# Add complex task with all options");
			AnsiConsole.WriteLine("task add --title \"Implement user authentication\" \\");
			AnsiConsole.WriteLine("         --description \"Add login/logout functionality with JWT tokens\" \\");
			AnsiConsole.WriteLine("         --priority high \\");
			AnsiConsole.WriteLine("         --due-date 2024-12-31 \\");
			AnsiConsole.WriteLine("         --tags backend,security,urgent \\");
			AnsiConsole.WriteLine("         --project auth-system \\");
			AnsiConsole.WriteLine("         --assignee john.doe");
			AnsiConsole.WriteLine("```");
			AnsiConsole.WriteLine();

			// Listing and Filtering Tasks
			AnsiConsole.MarkupLine("[yellow]Listing and Filtering Tasks[/]");
			AnsiConsole.WriteLine("```bash");
			AnsiConsole.WriteLine("# List all tasks");
			AnsiConsole.WriteLine("task list");
			AnsiConsole.WriteLine();
			AnsiConsole.WriteLine("# Filter by status and assignee");
			AnsiConsole.WriteLine("task list --status in_progress --assignee john.doe");
			AnsiConsole.WriteLine();
			AnsiConsole.WriteLine("# Show high priority tasks due this week");
			AnsiConsole.WriteLine("task list --priority high --due-before 2024-12-31");
			AnsiConsole.WriteLine();
			AnsiConsole.WriteLine("# JSON output for scripting");
			AnsiConsole.WriteLine("task list --json --status todo | jq '.[] | select(.priority == \"high\")'");
			AnsiConsole.WriteLine("```");
			AnsiConsole.WriteLine();

			// Task Modification
			AnsiConsole.MarkupLine("[yellow]Task Modification[/]");
			AnsiConsole.WriteLine("```bash");
			AnsiConsole.WriteLine("# Edit task title and priority");
			AnsiConsole.WriteLine("task edit abc123 --title \"Updated task name\" --priority low");
			AnsiConsole.WriteLine();
			AnsiConsole.WriteLine("# Change due date and add tags");
			AnsiConsole.WriteLine("task edit def456 --due-date 2024-11-15 --tags frontend,ui");
			AnsiConsole.WriteLine("```");
			AnsiConsole.WriteLine();

			// Search Functionality
			AnsiConsole.MarkupLine("[yellow]Search Functionality[/]");
			AnsiConsole.WriteLine("```bash");
			AnsiConsole.WriteLine("# Full-text search");
			AnsiConsole.WriteLine("task search \"authentication bug\"");
			AnsiConsole.WriteLine();
			AnsiConsole.WriteLine("# Semantic search for similar concepts");
			AnsiConsole.WriteLine("task search \"login issues\" --type semantic");
			AnsiConsole.WriteLine("```");
			AnsiConsole.WriteLine();

			// Bulk Operations
			AnsiConsole.MarkupLine("[yellow]Bulk Operations[/]");
			AnsiConsole.WriteLine("```bash");
			AnsiConsole.WriteLine("# Complete multiple tasks");
			AnsiConsole.WriteLine("task complete abc123 def456 ghi789");
			AnsiConsole.WriteLine();
			AnsiConsole.WriteLine("# Delete tasks with confirmation");
			AnsiConsole.WriteLine("task delete abc123 def456");
			AnsiConsole.WriteLine("```");
			AnsiConsole.WriteLine();

			// Data Export/Import
			AnsiConsole.MarkupLine("[yellow]Data Export/Import[/]");
			AnsiConsole.WriteLine("```bash");
			AnsiConsole.WriteLine("# Export to JSON");
			AnsiConsole.WriteLine("task export --format json --output tasks_backup.json");
			AnsiConsole.WriteLine();
			AnsiConsole.WriteLine("# Import from CSV");
			AnsiConsole.WriteLine("task import tasks_from_trello.csv");
			AnsiConsole.WriteLine("```");
			AnsiConsole.WriteLine();

			// Configuration Management
			AnsiConsole.MarkupLine("[yellow]Configuration Management[/]");
			AnsiConsole.WriteLine("```bash");
			AnsiConsole.WriteLine("# Set default output format");
			AnsiConsole.WriteLine("task config set default_output json");
			AnsiConsole.WriteLine();
			AnsiConsole.WriteLine("# View current settings");
			AnsiConsole.WriteLine("task config list");
			AnsiConsole.WriteLine();
			AnsiConsole.WriteLine("# Reset to defaults");
			AnsiConsole.WriteLine("task config unset default_output");
			AnsiConsole.WriteLine("```");
			AnsiConsole.WriteLine();

			// CONFIGURATION
			AnsiConsole.MarkupLine("[yellow]CONFIGURATION[/]");
			AnsiConsole.WriteLine("Task stores configuration in `~/.config/task/config.json`. Available settings include:");
			AnsiConsole.WriteLine();
			AnsiConsole.WriteLine("- `default_output`: Default output format (plain/json)");
			AnsiConsole.WriteLine("- `api_url`: Default API server URL");
			AnsiConsole.WriteLine("- `database_path`: Default database file location");
			AnsiConsole.WriteLine("- `auto_sync`: Enable automatic synchronization with remote server");
			AnsiConsole.WriteLine();
			AnsiConsole.WriteLine("Use `task config --help` for detailed configuration options.");
			AnsiConsole.WriteLine();

			// ENVIRONMENT VARIABLES
			AnsiConsole.MarkupLine("[yellow]ENVIRONMENT VARIABLES[/]");
			AnsiConsole.WriteLine("TASK_DB_PATH        Override default database path");
			AnsiConsole.WriteLine("TASK_API_URL        Override default API URL");
			AnsiConsole.WriteLine("NO_COLOR            Disable colored output (set to any non-empty value)");
			AnsiConsole.WriteLine("TERM=dumb           Force plain text output");
			AnsiConsole.WriteLine();

			// FILES
			AnsiConsole.MarkupLine("[yellow]FILES[/]");
			AnsiConsole.WriteLine("~/.config/task/config.json    User configuration file");
			AnsiConsole.WriteLine("tasks.db                      Local SQLite database (current directory by default)");
			AnsiConsole.WriteLine();

			// EXIT STATUS
			AnsiConsole.MarkupLine("[yellow]EXIT STATUS[/]");
			AnsiConsole.WriteLine("0      Success");
			AnsiConsole.WriteLine("1      General error (invalid arguments, task not found, etc.)");
			AnsiConsole.WriteLine("2      Configuration error");
			AnsiConsole.WriteLine("3      Network/API error");
			AnsiConsole.WriteLine();

			// DIAGNOSTICS
			AnsiConsole.MarkupLine("[yellow]DIAGNOSTICS[/]");
			AnsiConsole.WriteLine("Task provides detailed error messages with suggestions for correction. Use `--json` flag for structured error output suitable for programmatic handling.");
			AnsiConsole.WriteLine();

			// COMPATIBILITY
			AnsiConsole.MarkupLine("[yellow]COMPATIBILITY[/]");
			AnsiConsole.WriteLine("- Single file executable");
			AnsiConsole.WriteLine("- Compatible with Linux, macOS, and Windows");
			AnsiConsole.WriteLine("- SQLite database format version 3");
			AnsiConsole.WriteLine();

			// SEE ALSO
			AnsiConsole.MarkupLine("[yellow]SEE ALSO[/]");
			AnsiConsole.WriteLine("todo.txt(1), taskwarrior(1), trello-cli(1), github-issues(1)");
			AnsiConsole.WriteLine();

			// REPORTING BUGS
			AnsiConsole.MarkupLine("[yellow]REPORTING BUGS[/]");
			AnsiConsole.WriteLine("Report bugs at: https://github.com/jmkelly/task/issues");
			AnsiConsole.WriteLine();

			// DOCUMENTATION
			AnsiConsole.MarkupLine("[yellow]DOCUMENTATION[/]");
			AnsiConsole.WriteLine("Full documentation: https://github.com/jmkelly/docs/task-cli");
			AnsiConsole.WriteLine("API reference: https://opencode.ai/docs/task-api");
			AnsiConsole.WriteLine();

			// AUTHOR
			AnsiConsole.MarkupLine("[yellow]AUTHOR[/]");
			AnsiConsole.WriteLine("Written by the James Kelly.");
			AnsiConsole.WriteLine();

			// COPYRIGHT
			AnsiConsole.MarkupLine("[yellow]COPYRIGHT[/]");
			AnsiConsole.WriteLine("Copyright (C) 2026. License MIT.");
			AnsiConsole.WriteLine();
			AnsiConsole.WriteLine("Task CLI 1.0.0");

			return 0;
		}
	}
}
