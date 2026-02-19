# Task CLI

A cross-platform, self-contained CLI application for managing tasks using SQLite database.

## Features

- Add, list, edit, delete, and complete tasks
- Search tasks
- Export and import tasks
- JSON output for scripting and integration
- Plain text output option
- Custom database path support

## Installation

### Self-Contained Single File Executables

Download the appropriate single executable for your platform from the releases page. Each executable is self-contained, including the .NET runtime and all dependencies, making it easy to distribute and run without additional setup.

- **Linux x64**: `Task` (single executable file, ~17MB)
- **macOS Intel**: `Task` (single executable file)
- **macOS ARM64**: `Task` (single executable file)
- **Windows x64**: `Task.exe` (single executable file)

#### Installation Steps

1. **Download**: Download the executable for your platform from the [releases page](link-to-releases).

2. **Make Executable (Linux/macOS only)**: After downloading, make the file executable:
   ```bash
   chmod +x Task
   ```

3. **Install to PATH (Recommended)**: To use the `task` command from anywhere, move the executable to a directory in your system's PATH:
   - On Linux/macOS: Move to `/usr/local/bin/` (requires sudo) or `~/bin/` (create if needed):
     ```bash
     sudo mv Task /usr/local/bin/task
     # or
     mkdir -p ~/bin && mv Task ~/bin/task
     ```
     Ensure `~/bin` is in your PATH by adding to your shell profile (e.g., `~/.bashrc` or `~/.zshrc`):
     ```bash
     export PATH="$HOME/bin:$PATH"
     ```
   - On Windows: Move `Task.exe` to a directory in your PATH, such as `C:\Windows\System32\` or create a custom directory and add it to PATH via System Properties > Environment Variables.

4. **Verify Installation**: Open a new terminal and run:
   ```bash
   task --help
   ```

To run without installing to PATH, simply execute the file from its download location.

### Building from Source

Requirements:
- .NET 9.0 SDK

Clone the repository and build:

```bash
git clone <repository-url>
cd Task
dotnet build -c Release
```

To publish single-file executables:

```bash
# Linux
dotnet publish -c Release -r linux-x64 --self-contained -p:PublishSingleFile=true -p:PublishTrimmed=true

# macOS Intel
dotnet publish -c Release -r osx-x64 --self-contained -p:PublishSingleFile=true -p:PublishTrimmed=true

# macOS ARM64
dotnet publish -c Release -r osx-arm64 --self-contained -p:PublishSingleFile=true -p:PublishTrimmed=true

# Windows
dotnet publish -c Release -r win-x64 --self-contained -p:PublishSingleFile=true -p:PublishTrimmed=true
```

Run the application:

```bash
./Task --help
```

## Usage

### Basic Commands

Add a task:
```bash
task add "Buy groceries"
```

List tasks:
```bash
task list
```

Complete a task:
```bash
task complete 1
```

Edit a task:
```bash
task edit 1 "Buy groceries and milk"
```

Delete a task:
```bash
task delete 1
```

Search tasks:
```bash
task search "groceries"
```

### Options

- `--json`: Output in JSON format
- `--plain`: Plain text output
- `--db <path>`: Specify database file path (default: tasks.db)

### Examples

List tasks in JSON:
```bash
task list --json
```

Use a custom database:
```bash
task add --db mytasks.db "Custom database task"
```

## Database

The application uses SQLite for data storage. The database file is created automatically if it doesn't exist. The database is stored separately from the single-file executable in the current directory as `tasks.db` by default.