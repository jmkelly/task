# Task Management System

A cross-platform task management system with separate CLI client, REST API backend, and SQLite storage for advanced task management capabilities.

## Architecture

The system consists of three main components:

- **CLI Client** (`Task.Cli/`): A command-line interface for managing tasks
- **REST API Backend** (`Task.Api/`): A web API server providing REST endpoints
- **Storage**: SQLite database with full-text search and vector search capabilities

## Features

### CLI Features
- Add, list, edit, delete, and complete tasks
- Assign tasks to users/assignees
- Search tasks (full-text and semantic search)
- Export and import tasks
- JSON output for scripting and integration
- Plain text output option
- Custom database path support
- API integration mode (--api-url)

### API Features
- RESTful endpoints for task management
- Swagger/OpenAPI documentation
- CORS support for web integrations
- Advanced filtering and sorting
- Tag management
- JSON serialization with custom converters

### Storage Features
- SQLite database with schema including FTS5 and vector search
- Automatic schema migration
- Optimized indexes for performance
- Support for tags, priorities, due dates, assignees
- Full-text search on title, description, and tags
- Vector search for semantic similarity (requires sqlite-vss extension)

## Installation

### CLI Installation

#### Self-Contained Single File Executables

Download the appropriate single executable for your platform from the releases page. Each executable is self-contained, including the .NET runtime and all dependencies, making it easy to distribute and run without additional setup.

- **Linux x64**: `Task.Cli` (single executable file, ~17MB)
- **macOS Intel**: `Task.Cli` (single executable file)
- **macOS ARM64**: `Task.Cli` (single executable file)
- **Windows x64**: `Task.Cli.exe` (single executable file)

##### Installation Steps

1. **Download**: Download the executable for your platform from the [releases page](link-to-releases).

2. **Make Executable (Linux/macOS only)**: After downloading, make the file executable:
   ```bash
    chmod +x Task.Cli
    ```

3. **Install to PATH (Recommended)**: To use the `task` command from anywhere, move the executable to a directory in your system's PATH:
    - On Linux/macOS: Move to `/usr/local/bin/` (requires sudo) or `~/bin/` (create if needed):
      ```bash
      sudo mv Task.Cli /usr/local/bin/task
      # or
      mkdir -p ~/bin && mv Task.Cli ~/bin/task
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

#### Building CLI from Source

Requirements:
- .NET 8.0 SDK

Clone the repository and build:

```bash
git clone <repository-url>
cd Task
dotnet build Task.Cli/Task.Cli.csproj -c Release
```

To publish single-file executables:

```bash
cd Task.Cli

# Linux
dotnet publish -c Release -r linux-x64 --self-contained -p:PublishSingleFile=true -p:PublishTrimmed=true

# macOS Intel
dotnet publish -c Release -r osx-x64 --self-contained -p:PublishSingleFile=true -p:PublishTrimmed=true

# macOS ARM64
dotnet publish -c Release -r osx-arm64 --self-contained -p:PublishSingleFile=true -p:PublishTrimmed=true

# Windows
dotnet publish -c Release -r win-x64 --self-contained -p:PublishSingleFile=true -p:PublishTrimmed=true
```

Run the CLI application:

```bash
./Task.Cli --help
```

### API Backend Installation

#### Running via Docker Compose (Recommended)

The easiest way to run the API backend is using Docker Compose:

1. Ensure Docker and Docker Compose are installed on your system.

2. From the project root directory, run:
   ```bash
   docker-compose up -d
   ```

   This will:
   - Build the API Docker image
   - Start the API server on port 8080
   - Mount a `./data` directory for persistent SQLite database storage

3. Verify the API is running:
   - API endpoints: http://localhost:8080/api/tasks
   - Swagger UI: http://localhost:8080/swagger

4. To stop the services:
   ```bash
   docker-compose down
   ```

#### Building and Running API from Source

Requirements:
- .NET 8.0 SDK

1. Navigate to the API directory:
   ```bash
   cd Task.Api
   ```

2. Restore dependencies and run:
   ```bash
   dotnet run
   ```

The API will start on http://localhost:5000 (development) or https://localhost:5001 (with SSL).

For production deployment, configure the `ASPNETCORE_ENVIRONMENT` and `DatabasePath` settings.

## Usage

### CLI Usage

The CLI can operate in two modes:
- **Local Database Mode** (default): Uses a local SQLite database file
- **API Mode**: Connects to a remote API server using `--api-url`

#### Basic Commands

Add a task:
```bash
task add "Buy groceries"
task add "Review code" --assignee john.doe --priority high
```

List tasks:
```bash
task list
task list --assignee john.doe --status todo
```

Complete a task:
```bash
task complete 1
```

Edit a task:
```bash
task edit 1 "Buy groceries and milk"
task edit 1 --assignee jane.smith
```

Delete a task:
```bash
task delete 1
```

Search tasks:
```bash
task search "groceries"
```

#### Options

- `--json`: Output in JSON format
- `--plain`: Plain text output
- `--db <path>`: Specify database file path (default: tasks.db)
- `--api-url <url>`: Base URL of the Task API (switches to API mode)

#### Examples

List tasks in JSON:
```bash
task list --json
```

Use a custom database:
```bash
task add --db mytasks.db "Custom database task"
```

Connect to API backend:
```bash
task --api-url http://localhost:8080 list
```

### API Usage

The API provides REST endpoints for task management. When running locally, access:

- **Swagger UI**: http://localhost:8080/swagger (when using Docker) or http://localhost:5000/swagger (development)
- **API Base URL**: http://localhost:8080/api (Docker) or http://localhost:5000/api (development)

#### Key Endpoints

- `GET /api/tasks` - List all tasks with optional filtering
- `GET /api/tasks/{uid}` - Get a specific task by UID
- `POST /api/tasks` - Create a new task
- `PUT /api/tasks/{uid}` - Update a task
- `DELETE /api/tasks/{uid}` - Delete a task
- `PATCH /api/tasks/{uid}/complete` - Mark task as completed
- `GET /api/tasks/search?q={query}&type={fts|semantic}` - Search tasks
- `GET /api/tags` - Get all unique tags

#### API Examples

Create a task:
```bash
curl -X POST http://localhost:8080/api/tasks \
  -H "Content-Type: application/json" \
  -d '{"title": "New Task", "priority": "high"}'
```

List tasks:
```bash
curl http://localhost:8080/api/tasks
```

## Database

The application uses SQLite for data storage with an advanced schema supporting:

- **Tasks Table**: Core task data with priorities, due dates, tags, and status
- **FTS5 Virtual Table**: Full-text search across title, description, and tags
- **VSS Virtual Table**: Vector search for semantic similarity (requires sqlite-vss extension)
- **Triggers**: Automatic synchronization between tables
- **Indexes**: Optimized for common query patterns

The database file is created automatically. In Docker deployments, it's stored in the `./data` directory for persistence.

### Database Schema

See `SCHEMA.sql` for the complete database schema definition.