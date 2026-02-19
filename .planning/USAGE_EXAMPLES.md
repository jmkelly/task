# Usage Examples for Task

This provides practical examples for both human users and LLM agents integrating the tool.

## For Human Users

### Daily Workflow
1. Add tasks for the day:
   ```
   task add --title "Review code PRs" --description "Check team's pull requests" --priority high --tags "work,code"
   task add --title "Buy groceries" --priority medium --due-date 2024-02-19 --tags "personal,shopping"
   ```

2. List pending tasks:
   ```
   task list --status pending
   ```
   Shows a colored table with priorities highlighted.

3. Search for specific tasks:
   ```
   task search --query "code" --mode fts
   ```

4. Complete tasks:
   ```
   task complete --id a2b3k9
   ```

### Interactive Mode
For guided task creation:
```
task add
```
Follows prompts for each field with validation.

### Export for Backup
```
task export --format json --output ~/backups/tasks_2024-02-19.json
```

## For LLM Agents

### Structured JSON I/O
Agents can use --json for parseable output:
```
task list --json
```
Returns:
```json
[
  {
    "id": "a2b3k9",
    "title": "Review code PRs",
    "description": "Check team's pull requests",
    "priority": "high",
    "dueDate": "2024-02-20",
    "tags": ["work", "code"],
    "status": "pending",
    "createdAt": "2024-02-19T10:00:00Z",
    "updatedAt": "2024-02-19T10:00:00Z"
  }
]
```

### Scripting Integration
Chain commands for automation:
```
task add --title "Auto-generated task" --priority low --json > /dev/null && task list --status pending --plain
```
- First command adds task silently (output suppressed).
- Second lists in plain text for parsing.

### Error Handling
```
task add --title "" --json
```
Returns JSON error (exit code 2):
```json
{"error": {"type": "Validation", "message": "Title is required.", "suggestion": "Provide a non-empty title using --title.", "exitCode": 2}}
```

### Semantic Search for Natural Language
```
task search --query "things to buy" --mode semantic --json
```
Matches "buy groceries" even without exact words.

### Batch Import
```
cat tasks.json | task import --format json
```
Imports from piped JSON with progress bar.

## Advanced Scenarios

### Reporting
Generate stats:
```
task list --status completed --json | jq '. | group_by(.priority) | map({priority: .[0].priority, count: length})'
```
Uses jq to analyze completed tasks by priority.

### Cron Job for Overdue Alerts
```
task list --status pending --json | jq -r '.[] | select(.dueDate < "'$(date +%Y-%m-%d)'") | .title' | while read title; do echo "Overdue: $title"; done
```

### Integration with Other Tools
Export to CSV for spreadsheet analysis:
```
task export --format csv --output tasks.csv
```
Then open in Excel or Google Sheets.