# Error Codes for Task

The app uses standardized exit codes and error formats for reliable scripting and LLM integration. Errors follow the pattern: "ERROR: [Type] - [Message] - [Suggestion]".

## Exit Codes
- **0**: Success - Operation completed without issues.
- **1**: General Error - Unexpected failure (e.g., database corruption, system error).
- **2**: Invalid Input - User provided incorrect arguments or data (e.g., invalid priority, missing required fields).
- **3**: Not Found - Requested resource doesn't exist (e.g., task UUID (6-character alpha code) not found).
- **4**: Permission Denied - Access issue (rare for local SQLite, but possible for file access operations).
- **5**: Conflict - Operation would violate constraints (e.g., duplicate task if uniqueness enforced).

## Error Types and Examples

### Validation Errors (Exit Code 2)
- **ERROR: Validation - Title is required. - Provide a non-empty title using --title.**
- **ERROR: Validation - Invalid priority 'urgent'. Must be high/medium/low. - Use --priority high/medium/low.**
- **ERROR: Validation - Due date must be in YYYY-MM-DD format. - Example: --due-date 2024-02-20.**

### Database Errors (Exit Code 1)
- **ERROR: Database - Failed to connect to SQLite database. - Check database file path and permissions.**
- **ERROR: Database - Task insertion failed due to constraint violation. - Ensure unique titles or review schema.**

### Not Found Errors (Exit Code 3)
- **ERROR: NotFound - Task with UUID (6-character alpha code) a2b3k9 not found. - List tasks with 'task list' to verify UID (6-character alpha code)s.**
- **ERROR: NotFound - No tasks match search query. - Try broader terms or check spelling.**

### Semantic Search Errors (Exit Code 1)
- **ERROR: Embedding - Failed to generate embeddings. - Ensure SentenceTransformers.NET is installed and model loaded.**
- **ERROR: VectorSearch - sqlite-vss extension not available. - Fall back to FTS-only mode or install extension.**

## JSON Error Output
When --json flag is used, errors are output as JSON objects instead of plain text:
```json
{
  "error": {
    "type": "Validation",
    "message": "Invalid priority 'urgent'. Must be high/medium/low.",
    "suggestion": "Use --priority high/medium/low.",
    "exitCode": 2
  }
}
```

## Handling in Scripts/LLMs
- Check exit code after each command.
- Parse JSON errors if --json is enabled for structured handling.
- For piping, errors are written to stderr; successful output to stdout.
