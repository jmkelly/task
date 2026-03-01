# Task Plan

## Overview
This plan outlines the development of a CLI application for recording tasks in .NET C#. The app uses local SQLite storage with full-text search (FTS5) and semantic search (via vector embeddings with sqlite-vss). It optimizes UX for both humans (rich outputs with Spectre.Console) and LLM coding agents (structured JSON outputs, descriptive help/errors). Key goal: A tool that agents can easily integrate with for task management.

## Core Features
- **Basic Task Management**: Add, list, edit, delete, complete tasks. Tasks include: id, title, description, priority (high/medium/low), due date, tags, status (pending/completed).
- **Search Functionality**: Full-text search on title/description/tags using SQLite FTS5. Semantic search via vector embeddings for meaning-based matching (e.g., "get food" matches "buy groceries"). Hybrid mode combines both.
- **Data Persistence**: Store tasks in local SQLite DB (single file, portable). Includes creation/update timestamps.
- **Import**: Import with progress indicators.
- **Reporting**: CLI commands for stats (e.g., completed tasks by priority, overdue tasks).
- **Interactive Mode**: Guided prompts for task creation using Spectre.Console.

## Architecture Choices
- **CLI Framework**: Spectre.Console.Cli for command parsing and Spectre.Console for rich output (tables, colors, progress bars).
- **Data Storage**: SQLite with Microsoft.Data.Sqlite. FTS5 virtual table for full-text search. sqlite-vss extension for vector search on embeddings.
- **Project Structure**: Modular classes (e.g., data access, search logic, output renderers). Single executable.
- **Output Format**: Rich visuals by default for humans; --json flag for structured JSON (parseable by LLMs); --plain for text-only.
- **Error Handling**: Custom exception handlers with descriptive messages, exit codes (0=success, 1=error, 2=invalid input), and JSON errors if --json enabled.
- **Testing**: xUnit for unit/integration tests; Spectre's TestConsole for output testing.

## LLM Agent UX Optimizations
- **Structured I/O**: Commands support --json for JSON input/output. Idempotent operations (e.g., add checks for duplicates). Auto-detect piped input for plain mode.
- **Descriptive Help**: Auto-generated from attributes, with examples and styles. Includes agent-friendly notes (e.g., "Use --json for parseable output").
- **Descriptive Errors**: Format: "ERROR: [Type] - [Message] - [Suggestion]". Exit codes for scripting. JSON includes type/message/suggestion.
- **Other**: Command aliases, confirmations, scripting support (chain commands without rendering if piped).

## Implementation Roadmap
1. Setup Project: Initialize .NET CLI, add packages (Spectre, SQLite).
2. Core Data Layer: Define schema, implement CRUD.
3. Search Integration: FTS5, embeddings, vector search.
4. CLI Commands: Build with Spectre.Cli, add flags.
5. UX Polish: Rich outputs, conditional rendering, help, errors.
6. Testing & Validation: Write tests, benchmark.
7. Optimization: Tune pragmas, performance.
8. Documentation: Update README.

## Task List
Tasks are prioritized (1=highest/critical, 2=medium/features, 3=lowest/polish). Tackle 1 sequentially, 2+3 in parallel.

### Priority 1 (5 tasks)
- **setup-project**: Initialize .NET CLI project with Spectre.Console and Spectre.Console.Cli packages
- **setup-sqlite**: Install and configure SQLite packages (Microsoft.Data.Sqlite, sqlite-vss for semantic search)
- **design-schema**: Define SQLite schema: main tasks table, FTS5 virtual table for full-text search, and vss0 table for embeddings
- **implement-crud**: Implement core data layer: CRUD operations (add, edit, delete, complete) with prepared statements and transactions
- **build-cli-commands**: Build CLI commands (add, list, edit, delete, complete, search) using Spectre.Console.Cli with option flags (--json, --plain)

### Priority 2 (8 tasks)
- **implement-task-priority**: Implement task priority support: add priority field (high/medium/low) to tasks, update CRUD operations and CLI commands to handle priority input/output
- **implement-fts**: Implement FTS5 queries for full-text search on title/description/tags
- **implement-embeddings**: Set up embedding generation for semantic search (using SentenceTransformers.NET or similar)
- **implement-semantic-search**: Integrate vector search with sqlite-vss for semantic matching and hybrid search mode
- **implement-rich-outputs**: Implement rich outputs: tables for task lists, colors for status/priority, progress bars for operations
- **add-conditional-outputs**: Add conditional rendering: JSON output for --json flag, plain text for --plain or piped input
- **implement-errors**: Implement custom exception handlers with descriptive errors, exit codes, and JSON error output
- **write-tests**: Write unit tests for search accuracy, data operations; integration tests for CLI workflows using xUnit and Spectre's TestConsole

### Priority 3 (5 tasks)
- **configure-help**: Configure descriptive help: auto-generated from attributes with examples and styles for human/agent readability
- **add-interactive-mode**: Add interactive mode: prompts for task creation, multi-select for tags using Spectre.Console
- **implement-import**: Implement import command with progress indicators and JSON/CSV support
- **optimize-performance**: Tune SQLite pragmas (WAL mode, cache size) and benchmark search performance for large datasets
- **document-readme**: Update README with usage examples, agent-specific flags, and installation notes

## Potential Risks & Mitigations
- **Performance**: Semantic search CPU-intensive; mitigate with caching or simpler keyword expansion.
- **Dependencies**: sqlite-vss third-party; fall back to FTS-only if issues.
- **Compatibility**: Spectre detects terminals; plain fallback.
- **Scalability**: SQLite handles thousands of tasks; warn on large datasets.

## Next Steps
Start with Priority 1 tasks. Each task should be implemented with code comments, tests, and linting. Use `dotnet run` for testing. For commits, follow: "feat: [description]". No force pushes.

Ready for implementation!
