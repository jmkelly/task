# CLI Enhancements Implementation Plan

## Overview
Add 7 usability improvements to the Task CLI to make it easier to use.

## Implementation Order
1. Fix status filter in list command
2. Add --status flag to edit command
3. Add reset command
4. Add --status to add command
5. Batch operations
6. Shorter UID (6 characters)
7. Undo command (session-only)

---

## Feature 1: Fix status filter in list command

### Problem
The `--status` option exists but help shows "pending/completed" while DB uses "todo/done".

### Solution
Add status alias mapping in ListCommand: `pending→todo`, `completed→done`

### Files
- `Task/ListCommand.cs`

### Changes
- Add validation/conversion for status values
- Update help text to show valid values: `todo`, `done`, `in_progress`

---

## Feature 2: Add --status flag to edit command

### Problem
Cannot change task status via edit command, only via separate complete command.

### Solution
Add `-s|--status` option to EditCommand.

### Files
- `Task/EditCommand.cs`

### Changes
```csharp
[CommandOption("-s|--status")]
[Description("Update the status: todo, done, or in_progress")]
public string? Status { get; set; }
```

In ExecuteAsync:
- Validate status values: `todo`, `done`, `in_progress`
- If valid, set `task.Status = settings.Status`

---

## Feature 3: Add reset command

### Problem
No way to mark a done task back to todo without export/import workaround.

### Solution
Create new `reset` command that sets task status back to `todo`.

### Files
- `Task/ResetCommand.cs` (new)
- `Task/Program.cs`

### New Command Syntax
- `task reset <id>` - Reset single task to todo
- `task reset --all` - Reset all done tasks to todo

### Implementation
```csharp
public class ResetCommand : AsyncCommand<ResetCommand.Settings>
{
    public class Settings : Program.TaskCommandSettings
    {
        [CommandArgument(0, "<id>")]
        public string? Id { get; set; }
        
        [CommandOption("--all")]
        public bool All { get; set; }
    }
}
```

---

## Feature 4: Add --status to add command

### Problem
Cannot set initial status when creating a task.

### Solution
Add `-s|--status` option to AddCommand.

### Files
- `Task/AddCommand.cs`
- `Task/ITaskService.cs`
- `Task/Database.cs`
- `Task/ApiClient.cs`

### Changes
1. Add to AddCommand.Settings:
```csharp
[CommandOption("-s|--status")]
[Description("Initial status: todo, done, or in_progress (default: todo)")]
public string? Status { get; set; }
```

2. Update ITaskService.AddTaskAsync signature:
```csharp
Task<TaskItem> AddTaskAsync(string title, string? description, string priority, DateTime? dueDate, List<string> tags, string? status = "todo", CancellationToken cancellationToken = default);
```

3. Update Database.AddTaskAsync to accept and store status

---

## Feature 5: Batch operations

### Problem
Can only operate on one task at a time.

### Solution
Allow multiple IDs in complete, edit, and delete commands.

### Files
- `Task/CompleteCommand.cs`
- `Task/EditCommand.cs`
- `Task/DeleteCommand.cs`

### Changes
1. Change from single ID to array:
```csharp
[CommandArgument(0, "[ids]")]
public string[] Ids { get; set; } = Array.Empty<string>();
```

2. Loop through all IDs, process each, collect results

3. Add `--all` flag to CompleteCommand:
```csharp
[CommandOption("--all")]
public bool All { get; set; }
```

---

## Feature 6: Shorter UID (6 characters)

### Problem
8-character UIDs are unwieldy to type.

### Solution
Reduce UID to 6 characters, alphanumeric only (exclude confusing chars: 0,O,1,I,l).

### Files
- `Task/Database.cs`

### Changes
```csharp
private string GenerateUid()
{
    const string chars = "ABCDEFGHJKMNPQRSTUVWXYZ23456789"; // exclude 0,O,1,I,l
    var random = new Random();
    return new string(Enumerable.Range(0, 6)
        .Select(_ => chars[random.Next(chars.Length)])
        .ToArray());
}
```

Add retry logic for collisions (max 3 attempts).

---

## Feature 7: Undo command (session-only)

### Problem
No way to undo accidental operations.

### Solution
Store action history in memory (cleared on new CLI session).

### Files
- `Task/UndoCommand.cs` (new)
- `Task/Program.cs`
- `Task/Database.cs`
- `Task/ITaskService.cs`

### Action Types to Track
- `Complete` - stores UID
- `Delete` - stores full TaskItem (for restore)
- `Create` - stores UID (for deletion)
- `Edit` - stores before/after snapshot

### Command Syntax
- `task undo` - Undo last action
- `task undo --list` - Show available undo actions

### Implementation
- Add action stack to Database class (in-memory List)
- On undo: pop last action, reverse it
- On any new modifying command: clear undo stack

---

## Summary

| Feature | Files | Effort |
|---------|-------|--------|
| 1. Fix status filter | 1 edit | Small |
| 2. Edit --status | 1 edit | Small |
| 3. Reset command | 1 new, 1 edit | Small |
| 4. Add --status | 4 edits | Medium |
| 5. Batch ops | 3 edits | Medium |
| 6. Shorter UID | 1 edit | Medium |
| 7. Undo | 3 edits, 1 new | Large |

**Total:** 7 features, 14 file changes (8 edits, 2 new)
