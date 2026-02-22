# Plan: Improved `task add` Command, User-Friendly Errors, and Better Help Text

## 1. Overview
Enhance the Task CLI so users can:
- Add tasks quickly via `task add "some command"`
- Receive more detailed, actionable error messages
- Access a vastly improved `task --help` and `task add --help` with clear options, notes, and real-world examples

---

## 2. Quick Add Syntax: `task add "some command"`
- Permit a single positional argument (if not a flag) as a task title, e.g. `task add "Buy milk"`
- No required flags for the most basic use case
- Error if both a positional arg and --title are provided, with an explanation
- Default other fields (desc, priority, tags, etc.) when not given

---

## 3. Helpful Error Messaging
- On any CLI usage error (missing required, unknown flag, invalid value, etc):
  - Output a user-focused message:
    - Explain problem (“Title is required.”)
    - Suggest a fix ("Try: task add 'My task'")
    - Link to help ("See: task add --help")
- Example error messages:
    - Title missing: `Error: Title is required. Try: task add 'Task title'. See: task add --help.`
    - Invalid priority: `Error: 'urgent' is not a valid priority (must be: low, medium, high). See: task add --help.`
    - Both "Buy milk" positional and --title: `Error: Use only one of positional title or --title option.`
- Centralize formatting/styling of error output so all commands are consistent

---

## 4. Improved Help Text and Examples
### Goals:
- Rich `task --help` and `task add --help` output, including practical usage patterns, flags breakdown, and troubleshooting tips

### Example: `task add --help`
```
USAGE:
  task add "Task with only a title"
  task add --title "A task with options" --description "desc" --priority medium
  task add                    # interactive step-through

EXAMPLES:
  task add "Pay bills"
  task add --title "Refactor code" --tags code,refactor --priority high
  task add --title "Upload report" --due-date 2024-04-01

NOTES:
- Title is required. No title → error
- If both a positional title and --title are supplied, the command fails and offers a usage tip
- Use task add with no arguments for interactive mode
- For complete documentation on accepted priorities, tags or more, use task add --help

ERRORS:
- All input mistakes will be met with a user-friendly, actionable error and a concrete command example

RELATED:
  See: task list, task edit, task complete
```

---

## 5. Implementation Steps
1. Update parsing logic in `AddCommand.cs` to handle positional/flag title, disallow both
2. Extend error message formatting in error helpers for all CLI user errors
3. Rewrite help text descriptions in AddCommand and Program
4. Update tests to cover:
    - Quick Add
    - Failing/error cases (bad title, priority, combo, etc)
    - --help output
5. Revise usage and command documentation in README and .planning/USAGE_EXAMPLES.md

---

## 6. Acceptance Criteria
- `task add "X"` works as expected
- All errors give actionable feedback and point to --help
- Help output is clear, thorough, with many examples
- All supported add invocations and error situations are covered by tests
- Documentation stays in sync

---
# End of Plan
