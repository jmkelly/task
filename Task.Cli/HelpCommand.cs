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
			AnsiConsole.MarkupLine("[yellow]NAME[/]");
			AnsiConsole.WriteLine("task - A powerful, interactive CLI tool for efficient task and todo management");
			AnsiConsole.WriteLine();

			AnsiConsole.MarkupLine("[yellow]SYNOPSIS[/]");
			AnsiConsole.WriteLine("task [GLOBAL OPTIONS] <COMMAND> [COMMAND OPTIONS] [ARGS...]");
			AnsiConsole.WriteLine();

			AnsiConsole.MarkupLine("[yellow]DESCRIPTION[/]");
			AnsiConsole.WriteLine("Task is a modern command-line interface for managing personal and team tasks with advanced features like priorities, due dates, projects, tags, dependencies, and semantic search. The CLI requires a running Task API server; all operations are performed over HTTP using the API.");
			AnsiConsole.WriteLine();
			AnsiConsole.WriteLine("Key features include:");
			AnsiConsole.WriteLine("- Interactive and non-interactive task creation");
			AnsiConsole.WriteLine("- Flexible filtering and searching capabilities");
			AnsiConsole.WriteLine("- Priority and status management");
			AnsiConsole.WriteLine("- Project-based organization");
			AnsiConsole.WriteLine("- Dependency tracking");
			AnsiConsole.WriteLine("- JSON output for scripting and LLM integration");
			AnsiConsole.WriteLine("- Semantic search using vector embeddings");
			AnsiConsole.WriteLine("- Import functionality (JSON, CSV)");
			AnsiConsole.WriteLine("- Configuration management");
			AnsiConsole.WriteLine("- Bulk operations support");
			AnsiConsole.WriteLine();

			AnsiConsole.MarkupLine("[yellow]GLOBAL OPTIONS[/]");
			AnsiConsole.WriteLine("These options apply to all commands:");
			AnsiConsole.WriteLine();
			AnsiConsole.WriteLine("-h, --help          Display this help message and exit");
			AnsiConsole.WriteLine("-v, --version       Display version information and exit");
			AnsiConsole.WriteLine("--json              Output results in JSON format for scripting and LLM integration");
			AnsiConsole.WriteLine("--plain             Output in plain text format, disabling rich formatting and colors");
			AnsiConsole.WriteLine("--api-url <URL>     Base URL of the Task API server (required)");
			AnsiConsole.WriteLine();

			AnsiConsole.MarkupLine("[yellow]COMMANDS[/]");
			AnsiConsole.WriteLine();

			AnsiConsole.MarkupLine("[yellow]Task Management[/]");
			AnsiConsole.WriteLine("add                 Create a new task with optional properties like priority, due date, tags, and project assignment");
			AnsiConsole.WriteLine("list                Display tasks with advanced filtering by status, priority, assignee, project, tags, and due date");
			AnsiConsole.WriteLine("edit <UID>          Modify existing task properties (title, description, priority, due date, and assignee) (UID is a 6-letter code)");
			AnsiConsole.WriteLine("delete <UID...>      Permanently remove one or more tasks (supports bulk deletion with confirmation; UID is a 6-letter code, e.g., a2b3k9)");
			AnsiConsole.WriteLine("complete <UID...>    Mark tasks as completed (supports bulk completion; UID is a 6-letter code, e.g., a2b3k9)");
			AnsiConsole.WriteLine("reset <UID>         Reset a completed task back to pending status (UID is a 6-letter code)");
			AnsiConsole.WriteLine();

			AnsiConsole.MarkupLine("[yellow]Server[/]");
			AnsiConsole.WriteLine("start               Start the Task API server in the background");
			AnsiConsole.WriteLine("status              Show Task API server status");
			AnsiConsole.WriteLine("stop                Stop the Task API server");
			AnsiConsole.WriteLine("server run          Run the Task API server in the foreground");
			AnsiConsole.WriteLine("  Options:");
			AnsiConsole.WriteLine("    --urls <URLS>                  Override server URLs (e.g., http://localhost:8080). Disables port auto-selection.");
			AnsiConsole.WriteLine("    --database-provider <PROVIDER> Database provider for the API server (sqlite or pg). Default: sqlite.");
			AnsiConsole.WriteLine("    --database-path <PATH>         SQLite database path for the API server (default: config dir tasks.db, e.g. ~/.config/task/tasks.db).");
			AnsiConsole.WriteLine("    --pg-connection-string <VALUE> PostgreSQL connection string for the API server when provider is pg.");
			AnsiConsole.WriteLine("    --ready-file <PATH>            Write readiness details to this file once the server is ready.");
			AnsiConsole.WriteLine();
			AnsiConsole.WriteLine("server start        Start the Task API server in the background");
			AnsiConsole.WriteLine("server status       Show Task API server status");
			AnsiConsole.WriteLine("server stop         Stop the Task API server");
			AnsiConsole.WriteLine();

			AnsiConsole.MarkupLine("[yellow]Search and Discovery[/]");
			AnsiConsole.WriteLine("search <QUERY>      Perform full-text or semantic similarity search across task titles and descriptions");
			AnsiConsole.WriteLine();

			AnsiConsole.MarkupLine("[yellow]Data Management[/]");
			AnsiConsole.WriteLine("import <FILE>       Import tasks from JSON or CSV files, merging with existing data");
			AnsiConsole.WriteLine();

			AnsiConsole.MarkupLine("[yellow]Configuration[/]");
			AnsiConsole.WriteLine("config              Manage CLI configuration settings");
			AnsiConsole.WriteLine("  set <KEY> <VALUE> Set a configuration value");
			AnsiConsole.WriteLine("  get <KEY>         Retrieve a configuration value");
			AnsiConsole.WriteLine("  unset <KEY>       Remove a configuration setting");
			AnsiConsole.WriteLine("  list              Display all current configuration settings");
			AnsiConsole.WriteLine();

			AnsiConsole.MarkupLine("[yellow]EXAMPLES[/]");
			AnsiConsole.WriteLine();
			AnsiConsole.MarkupLine("[yellow]Server Mode[/]");
			AnsiConsole.WriteLine("Note: 'server run' starts the API server in the foreground (blocking in the current shell), whereas 'server start' runs it detached in the background.");
			AnsiConsole.WriteLine();
			AnsiConsole.WriteLine("```bash");
			AnsiConsole.WriteLine("# Run API server in the foreground with defaults");
			AnsiConsole.WriteLine("task server run");
			AnsiConsole.WriteLine("# Default database provider is sqlite and default database path resolves to ~/.config/task/tasks.db");
			AnsiConsole.WriteLine();
			AnsiConsole.WriteLine("# Run with custom sqlite path");
			AnsiConsole.WriteLine("task server run --database-provider sqlite --database-path tasks_team.db");
			AnsiConsole.WriteLine();
			AnsiConsole.WriteLine("# Run with PostgreSQL");
			AnsiConsole.WriteLine("task server run --database-provider pg --pg-connection-string \"Host=localhost;Username=task;Password=secret;Database=task\"");
			AnsiConsole.WriteLine();
			AnsiConsole.WriteLine("# Run with custom URL and readiness file");
			AnsiConsole.WriteLine("task server run --urls http://localhost:9090 --ready-file ./api.ready.json");
			AnsiConsole.WriteLine("```");
			AnsiConsole.WriteLine();

			AnsiConsole.MarkupLine("[yellow]Configuration Management[/]");
			AnsiConsole.WriteLine("```bash");
			AnsiConsole.WriteLine("# Set default output format");
			AnsiConsole.WriteLine("task config set defaultOutput json");
			AnsiConsole.WriteLine("# Select sqlite explicitly and set its path");
			AnsiConsole.WriteLine("task config set database.provider sqlite");
			AnsiConsole.WriteLine("task config set database.sqlite.path ~/.config/task/tasks.db");
			AnsiConsole.WriteLine("# Select PostgreSQL and set its connection string");
			AnsiConsole.WriteLine("task config set database.provider pg");
			AnsiConsole.WriteLine("task config set database.pg.connectionString \"Host=localhost;Username=task;Password=secret;Database=task\"");
			AnsiConsole.WriteLine("# Set Telegram bot token and chat ID");
			AnsiConsole.WriteLine("task config set telegram.botToken <YOUR-BOT-TOKEN>");
			AnsiConsole.WriteLine("task config set telegram.chatId <YOUR-CHAT-ID>");
			AnsiConsole.WriteLine();
			AnsiConsole.WriteLine("# Get database config values");
			AnsiConsole.WriteLine("task config get database.provider");
			AnsiConsole.WriteLine("task config get database.sqlite.path");
			AnsiConsole.WriteLine("task config get database.pg.connectionString");
			AnsiConsole.WriteLine();
			AnsiConsole.WriteLine("# Reset provider to default sqlite");
			AnsiConsole.WriteLine("task config unset database.provider");
			AnsiConsole.WriteLine("```");
			AnsiConsole.WriteLine();

			AnsiConsole.MarkupLine("[yellow]CONFIGURATION[/]");
			AnsiConsole.WriteLine("Task stores configuration in `~/.config/task/config.json`. Available settings include:");
			AnsiConsole.WriteLine();
			AnsiConsole.WriteLine("- `defaultOutput`: Default output format (plain/json)");
			AnsiConsole.WriteLine("- `apiUrl`: Default API server URL");
			AnsiConsole.WriteLine("- `database.provider`: Database provider (`sqlite` or `pg`). Defaults to `sqlite`");
			AnsiConsole.WriteLine("- `database.sqlite.path`: SQLite database path used when provider is `sqlite`");
			AnsiConsole.WriteLine("- `database.postgres.connectionString`: PostgreSQL connection string used when provider is `pg`");
			AnsiConsole.WriteLine("- `telegram`: Telegram bot configuration object");
			AnsiConsole.WriteLine();
			AnsiConsole.WriteLine("Example:");
			AnsiConsole.WriteLine("{");
			AnsiConsole.WriteLine("  \"apiUrl\": \"http://localhost:8080\",");
			AnsiConsole.WriteLine("  \"defaultOutput\": \"plain\",");
			AnsiConsole.WriteLine("  \"database\": {");
			AnsiConsole.WriteLine("    \"provider\": \"sqlite\",");
			AnsiConsole.WriteLine("    \"sqlite\": {");
			AnsiConsole.WriteLine("      \"path\": \"~/.config/task/tasks.db\"");
			AnsiConsole.WriteLine("    },");
			AnsiConsole.WriteLine("    \"postgres\": {");
			AnsiConsole.WriteLine("      \"connectionString\": \"Host=localhost;Username=task;Password=secret;Database=task\"");
			AnsiConsole.WriteLine("    }");
			AnsiConsole.WriteLine("  },");
			AnsiConsole.WriteLine("  \"telegram\": {");
			AnsiConsole.WriteLine("    \"botToken\": \"<your-bot-token>\",");
			AnsiConsole.WriteLine("    \"chatId\": \"<your-chat-id>\"");
			AnsiConsole.WriteLine("  }");
			AnsiConsole.WriteLine("}");
			AnsiConsole.WriteLine();

			AnsiConsole.MarkupLine("[yellow]FILES[/]");
			AnsiConsole.WriteLine("~/.config/task/config.json    User configuration file");
			AnsiConsole.WriteLine("~/.config/task/tasks.db       Default sqlite database file when database.provider is sqlite and no path override is set");
			AnsiConsole.WriteLine();

			AnsiConsole.MarkupLine("[yellow]EXIT STATUS[/]");
			AnsiConsole.WriteLine("0      Success");
			AnsiConsole.WriteLine("1      General error (invalid arguments, task not found, etc.)");
			AnsiConsole.WriteLine("2      Configuration error");
			AnsiConsole.WriteLine("3      Network/API error");
			AnsiConsole.WriteLine();

			AnsiConsole.WriteLine("Task CLI 1.0.0");
			return 0;
		}
	}
}
