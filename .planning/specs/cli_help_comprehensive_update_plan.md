# Comprehensive Task CLI Help Output Update Plan

## Overview
This plan outlines the steps to update the Task CLI `--help` output to be as comprehensive as possible, following verbose, modern CLI standards with workflow examples, troubleshooting, and user-friendly formatting.

## Preferences Incorporated
- **Verbose**: Detailed descriptions, long-form explanations, and comprehensive examples.
- **Workflow-Inclusive**: Includes real-world scenarios, flag combinations, scripting, and integration examples.
- **Formatting & Extra Sections**: Decided based on best practices—readable indentation, tabular summaries where feasible, emphasis (bold/italics), and troubleshooting/edge cases sections.

## 1. Review the Actual CLI's Supported Commands, Flags, and Arguments
- Enumerate all commands, subcommands, global options, command-specific options, positional arguments, and supported workflow/combinations from the CLI source code or runtime help output.
- Capture edge-case and error behaviors (e.g., invalid inputs, missing required args, database connection issues).

## 2. Analyze the Current --help Output
- Run `task --help` and every `task <command> --help` variant to capture and document the output structure and completeness.
- List gaps or ambiguous sections, such as:
  - Undocumented commands/options.
  - Flags without descriptions, allowed values, or defaults.
  - Missing usage examples or workflow scenarios.
  - Inadequate error/help output for invalid input.

## 3. Research Best-Practice CLI Help Patterns
- Review top CLIs (e.g., `gh`, `git`, `docker`, `kubectl`, `aws`) for inspiration on:
  - **Organization**: Grouped sections (commands, global options, per-command options, examples, troubleshooting).
  - **Option Details**: Clear explanations, allowed values, defaults, and purposes.
  - **Examples**: At least one per command, plus complex workflows (e.g., API mode, custom DB).
  - **Formatting**: Consistent indentation, alignment, and emphasis (bold for headers, italics for notes).
  - **Error Handling**: What’s shown on invalid input, common pitfalls.
  - **Footers**: Links to external docs or README.

## 4. Draft a World-Class, Verbose --help Output Structure
Create a new help output with the following sections:

- **Header**: Brief intro, version, and copyright.
- **Usage**: Syntax summary with invocation patterns.
- **Commands**: All main/subcommands, each with a one-liner and long-form description.
- **Global Options**: All global flags/options with purpose, allowed values, and defaults.
- **Command-Specific Options**: Detailed per-command options with context and inline examples.
- **Workflow Examples**: Real-world scenarios, including flag combinations, scripting, switching DB/API modes.
- **Tabular Option Summaries**: For readability (use tables if CLI output supports Markdown-like formatting).
- **Troubleshooting/Edge Cases**: Common errors, invalid input guidance, and resolution tips.
- **Footer**: Reference to README/docs and support links.

## 5. Validate for Completeness and Usability
- Ensure every command/flag has at least one example, preferably multiple for workflows.
- Confirm descriptions are verbose, unambiguous, and cover edge cases.
- Validate formatting for readability (e.g., indentation, bold headers).
- Test that error messages and troubleshooting align with actual CLI behavior.

## Example Output Structure
```
Task CLI v1.0.0 - Cross-platform task manager

USAGE:
  task <command> [arguments] [options]

COMMANDS:
  add        Add a new task to the list.
             *Long-form*: Use to create a task, optionally assigning, setting priority, tags, or due date.

  list       List tasks. Supports filtering by assignee, status, tag, priority, due date.
             *Long-form*: List all, filter, and sort tasks. Use with output formatting and DB/API options.

  edit       Edit an existing task. Update title, description, assignee, tags, priority, due date.

  complete   Mark task as completed (supports bulk complete).

  delete     Delete tasks from database/API.

  search     Search tasks using full-text or semantic similarity.

  ...        (Other commands/subcommands as found in actual CLI)

GLOBAL OPTIONS:
  --db <path>        Specify alternative database file. *Default*: tasks.db
  --api-url <url>    Use remote API backend instead of local DB
  --json             Output in JSON format (for scripting/integration)
  --plain            Output in plain text format
  --help             Show this help message or command-specific help
  --version          Show CLI version

COMMAND-SPECIFIC OPTIONS (examples):
  add:
    --assignee <name>     Assign task to user/assignee
    --priority <level>    Set priority: high | medium | low (*default*: medium)
    --tag <tag>           Add tag(s) to task
    --due <date>          Set due date (YYYY-MM-DD)

  list:
    --assignee <name>     Filter by assignee
    --status <todo|done>  Filter by status
    --tag <tag>           Filter by tag(s)
    --sort <field>        Sort: priority | due | created

WORKFLOW EXAMPLES:
  task add "Buy groceries" --priority high --assignee john.doe --tag errands --due 2026-03-01
  task list --status todo --assignee john.doe --json --sort due
  task complete 123 456 789
  task edit 321 --assignee jane.smith --priority low
  task search "quarterly review"
  task --db custom_tasks.db list --json
  task --api-url http://localhost:8080 list --assignee team_lead
  task add --db team.db "Sync with product managers" --assignee pm

TROUBLESHOOTING & EDGE CASES:
  - If you receive 'file not found', verify database path with --db.
  - For 'invalid flag' errors, see available options above or use --help with specific command.
  - To resolve API connection errors, check --api-url and network status.
  - Task IDs must be numeric.
  - Dates should be formatted as YYYY-MM-DD.

For more info, visit docs: https://example.com/docs/task-cli or README.md
```

## Recommendations
- **Formatting**: Use indentation and emphasis (e.g., *italics* for long-form or defaults). If CLI supports color, use it for section headers and errors.
- **Sections**: Include all outlined sections for completeness.
- **Troubleshooting**: Cover the most frequent errors based on CLI analysis.
- **Links**: Include a footer link to docs for extended help.
- **Examples**: Provide 1-2 examples per command, emphasizing workflows.

## Next Steps
- Implement the plan by reviewing actual CLI help output.
- Update the CLI source code to generate this help text.
- Test and iterate based on user feedback.</content>
<parameter name="filePath">/home/james/Documents/code/Task/.planning/specs/cli_help_comprehensive_update_plan.md