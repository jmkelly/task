using Microsoft.Extensions.DependencyInjection;
using Spectre.Console;
using Spectre.Console.Cli;
using System.ComponentModel;
using Task.Core;

namespace Task.Cli
{
	public class HelpCommand : AsyncCommand<HelpCommand.Settings>
	{
		public class Settings : CommandSettings
		{
			[CommandArgument(0, "[command]")]
			[Description("Optional command to show help for (e.g., 'task help add'). Omit to show the full manual.")]
			public string? Command { get; set; }
		}

		public override async Task<int> ExecuteAsync(CommandContext context, Settings settings, CancellationToken cancellationToken)
		{
			if (!string.IsNullOrEmpty(settings.Command))
			{
				return await ShowCommandHelpAsync(settings.Command);
			}

			var version = typeof(Program).Assembly.GetName().Version?.ToString() ?? "unknown";

			AnsiConsole.MarkupLine("[yellow]NAME[/]");
			AnsiConsole.WriteLine("task - A powerful, interactive CLI tool for efficient task and todo management");
			AnsiConsole.WriteLine();

			AnsiConsole.MarkupLine("[yellow]SYNOPSIS[/]");
			AnsiConsole.WriteLine("task [GLOBAL OPTIONS] <COMMAND> [COMMAND OPTIONS] [ARGS...]");
			AnsiConsole.WriteLine();

			AnsiConsole.MarkupLine("[yellow]DESCRIPTION[/]");
			AnsiConsole.WriteLine("Task is a modern command-line interface for managing personal and team tasks with advanced features like priorities, due dates, projects, tags, dependencies, and semantic search. The CLI requires a running Task API server; all operations are performed over HTTP using the API.");
			AnsiConsole.WriteLine();
			AnsiConsole.WriteLine("Run 'task <command> --help' for detailed options of a specific command.");
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
			AnsiConsole.WriteLine("These options apply to the CLI itself:");
			AnsiConsole.WriteLine();
			AnsiConsole.WriteLine("-h, --help          Display this help message and exit");
			AnsiConsole.WriteLine("-v, --version       Display version information and exit");
			AnsiConsole.WriteLine();

			AnsiConsole.MarkupLine("[yellow]COMMON TASK COMMAND OPTIONS[/]");
			AnsiConsole.WriteLine("These options apply to the task commands (add, list, edit, delete, complete, reset, search, import), not to config, users, server, or telegram commands:");
			AnsiConsole.WriteLine();
			AnsiConsole.WriteLine("--json              Output results in JSON format for scripting and LLM integration");
			AnsiConsole.WriteLine("--plain             Output in plain text format, disabling rich formatting and colors");
			AnsiConsole.WriteLine("--api-url <URL>     Base URL of the Task API server (defaults to config api-url)");
			AnsiConsole.WriteLine();

			AnsiConsole.MarkupLine("[yellow]COMMANDS[/]");
			AnsiConsole.WriteLine();

			AnsiConsole.MarkupLine("[yellow]Task Management[/]");
			WriteCommand("add", "Create a new task with optional properties like priority, due date, tags, and project assignment");
			WriteCommand("list", "Display tasks with advanced filtering by status, priority, assignee, project, tags, and due date");
			WriteCommand("edit <ids>", "Modify existing task properties (title, description, priority, due date, tags, project, assignee, status) for one or more tasks (6-letter UID, e.g., a2b3k9)");
			WriteCommand("delete <ids>", "Archive one or more tasks (removes them from active lists; supports bulk deletion; 6-letter UID, e.g., a2b3k9)");
			WriteCommand("complete <ids>", "Mark tasks as completed (supports bulk completion; --all completes all todo tasks; 6-letter UID, e.g., a2b3k9)");
			WriteCommand("reset [id]", "Reset a completed task back to todo, or use --all to reset all done tasks (6-letter UID, e.g., a2b3k9)");
			WriteCommand("search <query>", "Perform full-text or semantic similarity search across task titles and descriptions");
			WriteCommand("import <input>", "Import tasks from a JSON or CSV file (imported tasks get new UIDs)");
			AnsiConsole.WriteLine();

			AnsiConsole.MarkupLine("[yellow]Server[/]");
			WriteCommand("start", "Start the Task API server in the background (alias for 'server start')");
			WriteCommand("status", "Show Task API server status (alias for 'server status')");
			WriteCommand("stop", "Stop the Task API server (alias for 'server stop')");
			WriteCommand("server run", "Run the Task API server in the foreground");
			AnsiConsole.WriteLine("  Options:");
			AnsiConsole.WriteLine("    --urls <URLS>                  Override server URLs (e.g., http://localhost:8080). Disables port auto-selection.");
			AnsiConsole.WriteLine("    --database-provider <PROVIDER> Database provider for the API server (sqlite or pg). Default: sqlite.");
			AnsiConsole.WriteLine("    --database-path <PATH>         SQLite database path for the API server (default: config dir tasks.db, e.g. ~/.config/task/tasks.db).");
			AnsiConsole.WriteLine("    --pg-connection-string <VALUE> PostgreSQL connection string for the API server when provider is pg.");
			AnsiConsole.WriteLine("    --ready-file <PATH>            Write readiness details to this file once the server is ready.");
			AnsiConsole.WriteLine();
			WriteCommand("server start", "Start the Task API server in the background (same as 'task start')");
			WriteCommand("server status", "Show Task API server status (same as 'task status')");
			WriteCommand("server stop", "Stop the Task API server (same as 'task stop')");
			AnsiConsole.WriteLine();

			AnsiConsole.MarkupLine("[yellow]Account Management[/]");
			WriteCommand("users create", "Create a user account directly in the database (local, no API required; works before the first signup and with closed signups)");
			AnsiConsole.WriteLine("  Options:");
			AnsiConsole.WriteLine("    --password <PASSWORD>   Password (prompted when omitted)");
			AnsiConsole.WriteLine("    --admin                  Grant the admin role (needed to sign in when signups are closed)");
			AnsiConsole.WriteLine();

			AnsiConsole.MarkupLine("[yellow]Configuration[/]");
			WriteCommand("config", "Manage CLI configuration settings");
			AnsiConsole.WriteLine("  set <KEY> <VALUE> Set a configuration value");
			AnsiConsole.WriteLine("  get <KEY>         Retrieve a configuration value");
			AnsiConsole.WriteLine("  unset <KEY>       Remove a configuration setting");
			AnsiConsole.WriteLine("  list              Display all current configuration settings");
			AnsiConsole.WriteLine();

			AnsiConsole.MarkupLine("[yellow]Telegram[/]");
			WriteCommand("telegram discover-chat-id", "Discover recent Telegram chat IDs from your bot and set telegram.chatId interactively");
			AnsiConsole.WriteLine();

			AnsiConsole.MarkupLine("[yellow]General[/]");
			WriteCommand("help", "Show detailed help, optionally for a specific command (e.g., 'task help add')");
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
			AnsiConsole.WriteLine("task server run --database-provider pg --pg-connection-string \"Host=localhost;Username=task;Password=<your-password>;Database=task\"");
			AnsiConsole.WriteLine();
			AnsiConsole.WriteLine("# Run with custom URL and readiness file");
			AnsiConsole.WriteLine("task server run --urls http://localhost:9090 --ready-file ./api.ready.json");
			AnsiConsole.WriteLine("```");
			AnsiConsole.WriteLine();

			AnsiConsole.MarkupLine("[yellow]Configuration Management[/]");
			AnsiConsole.WriteLine("```bash");
			AnsiConsole.WriteLine("# Set default output format");
			AnsiConsole.WriteLine("task config set defaultOutput json");
			AnsiConsole.WriteLine("# Store your API key (create one on the board at /keys)");
			AnsiConsole.WriteLine("task config set api.key tk_...");
			AnsiConsole.WriteLine("# Or use the TASK_API_KEY environment variable instead (takes precedence)");
			AnsiConsole.WriteLine("# Select sqlite explicitly and set its path");
			AnsiConsole.WriteLine("task config set database.provider sqlite");
			AnsiConsole.WriteLine("task config set database.sqlite.path ~/.config/task/tasks.db");
			AnsiConsole.WriteLine("# Select PostgreSQL and set its connection string");
			AnsiConsole.WriteLine("task config set database.provider pg");
			AnsiConsole.WriteLine("task config set database.pg.connectionString \"Host=localhost;Username=task;Password=<your-password>;Database=task\"");
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

			AnsiConsole.MarkupLine("[yellow]AUTHENTICATION[/]");
			AnsiConsole.WriteLine("Every Task server requires authentication. The browser signs in with a username and");
			AnsiConsole.WriteLine("password; the CLI and AI agents authenticate with a per-user API key.");
			AnsiConsole.WriteLine();
			AnsiConsole.WriteLine("First run:");
			AnsiConsole.WriteLine("  1. Start the server: task server run");
			AnsiConsole.WriteLine("  2. Open the board in a browser and create the first account (it becomes admin).");
			AnsiConsole.WriteLine("  3. Open /keys, create an API key, copy it (shown exactly once).");
			AnsiConsole.WriteLine("  4. Configure the CLI: task config set api.key <key>");
			AnsiConsole.WriteLine("     (or export TASK_API_KEY=<key> — the env var takes precedence).");
			AnsiConsole.WriteLine();
			AnsiConsole.WriteLine("401 responses tell you exactly what to do. Revoking a key on the /keys page takes");
			AnsiConsole.WriteLine("effect immediately; the CLI fails with an actionable message until a new key is set.");
			AnsiConsole.WriteLine();
			AnsiConsole.WriteLine("Closed signup: set Auth__AllowSignup=false on the server and create accounts with");
			AnsiConsole.WriteLine("`task users create <username> --admin` (local, uses the configured database).");
			AnsiConsole.WriteLine();

			AnsiConsole.MarkupLine("[yellow]CONFIGURATION[/]");
			AnsiConsole.WriteLine("Task stores configuration in `~/.config/task/config.json`. Available settings include:");
			AnsiConsole.WriteLine();
			AnsiConsole.WriteLine("- `defaultOutput`: Default output format (plain/json)");
			AnsiConsole.WriteLine("- `apiUrl`: Default API server URL");
			AnsiConsole.WriteLine("- `api.key`: Per-user API key sent as the X-Api-Key header (TASK_API_KEY env var overrides)");
			AnsiConsole.WriteLine("- `database.provider`: Database provider (`sqlite` or `pg`). Defaults to `sqlite`");
			AnsiConsole.WriteLine("- `database.sqlite.path`: SQLite database path used when provider is `sqlite`");
			AnsiConsole.WriteLine("- `database.pg.connectionString`: PostgreSQL connection string used when provider is `pg` (stored under `database.postgres` in config.json; `database.postgres.connectionString` is accepted as an alias)");
			AnsiConsole.WriteLine("- `telegram`: Telegram bot configuration object");
			AnsiConsole.WriteLine();
			AnsiConsole.WriteLine("Example:");
			AnsiConsole.WriteLine("{");
			AnsiConsole.WriteLine("  \"apiUrl\": \"http://localhost:8080\",");
			AnsiConsole.WriteLine("  \"apiKey\": \"tk_...\",");
			AnsiConsole.WriteLine("  \"defaultOutput\": \"plain\",");
			AnsiConsole.WriteLine("  \"database\": {");
			AnsiConsole.WriteLine("    \"provider\": \"sqlite\",");
			AnsiConsole.WriteLine("    \"sqlite\": {");
			AnsiConsole.WriteLine("      \"path\": \"~/.config/task/tasks.db\"");
			AnsiConsole.WriteLine("    },");
			AnsiConsole.WriteLine("    \"postgres\": {");
			AnsiConsole.WriteLine("      \"connectionString\": \"Host=localhost;Username=task;Password=<your-password>;Database=task\"");
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
			AnsiConsole.WriteLine("1      Error (invalid arguments, task not found, API or network errors)");
			AnsiConsole.WriteLine();

			AnsiConsole.WriteLine($"Task CLI {version}");
			return 0;
		}

		private static async Task<int> ShowCommandHelpAsync(string command)
		{
			var services = new ServiceCollection();
			services.AddSingleton<IUid, Uid>();
			var app = new CommandApp(new TypeRegistrar(services));
			app.Configure(Program.ConfigureApp);

			int result;
			try
			{
				result = await app.RunAsync(new[] { command, "--help" });
			}
			catch (Exception)
			{
				Console.Error.WriteLine($"Error: Unknown command '{command}'.");
				Console.Error.WriteLine("Run 'task --help' to list available commands.");
				return 1;
			}

			if (result != 0)
			{
				Console.Error.WriteLine($"Run 'task --help' to list available commands.");
			}

			return result;
		}

		private static void WriteCommand(string name, string description)
		{
			AnsiConsole.WriteLine($"{name.PadRight(26)}{description}");
		}
	}
}
