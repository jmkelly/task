# Example Commands for Task

These are sample CLI invocations demonstrating usage of the app. Assume the executable is named `task` and built with `dotnet run` or published.

## Basic Task Management

### Add a Task (Interactive Mode)
```
task add
```
Prompts for title, description, priority, due date, and tags interactively.

### Add a Task (Command Line)
```
task add --title "Buy groceries" --description "Milk, bread, eggs" --priority high --due-date 2024-02-20 --tags "shopping,urgent"
```

### List All Tasks
```
task list
```
Displays tasks in a rich table format.

### List Tasks with JSON Output (LLM-friendly)
```
task list --json
```
Outputs structured JSON array of tasks.

### Edit a Task
```
task edit --id a2b3k9 --title "Updated title" --priority medium
```

### Complete a Task
```
task complete a2b3k9
```

### Delete a Task
```
task delete --id a2b3k9
```

## Search Functionality

### Full-Text Search
```
task search --query "groceries" --mode fts
```

### Semantic Search
```
task search --query "get food" --mode semantic
```
Finds tasks related to food even if not using the exact word.

### Hybrid Search (FTS + Semantic)
```
task search --query "urgent tasks" --mode hybrid
```

### Search with Filters
```
task search --query "work" --status pending --priority high --json
```

## Export and Import

### Export to JSON
```
task export --format json --output tasks_backup.json
```

### Import from CSV
```
task import --format csv --input tasks.csv
```

## Other Examples

### Plain Text Output (for scripting)
```
task list --plain
```
Or automatically detects piped input: `task list | cat`

### Get Help
```
task --help
```
Or for specific command: `task add --help`

### Error Example
```
task add --title "" --priority invalid
```
Returns: ERROR: Validation - Invalid priority. Must be high/medium/low. -- Exit code 2

### Scripting Chain (no rendering if piped)
```
task add --title "Task 1" --json | task add --title "Task 2" --json
```