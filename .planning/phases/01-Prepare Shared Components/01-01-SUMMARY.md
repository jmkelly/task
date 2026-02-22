---
phase: 1
plan: 1
subsystem: Core
tags: [csharp, dotnet, shared-library, database, api]
requires: []
provides: [Task.Core library with unified TaskItem, ITaskService, DTOs, Database logic]
affects: [CLI, API projects]
tech-stack:
  added: [Microsoft.Data.Sqlite]
  patterns: [Shared library, Repository pattern]
key-files:
  created: [Task.Core/TaskItem.cs, Task.Core/ITaskService.cs, Task.Core/DTOs.cs, Task.Core/Database.cs, Task.Core/DateTimeNullableConverter.cs, Task.Core/SCHEMA.sql, Task.Core/MIGRATION_KANBAN.sql, Task.Core/vss0.so, Task.Core/vector0.so, Task.Core/libe_sqlite3.so, nupkg/Task.Core.1.0.0.nupkg]
  modified: [Task/Task.csproj, Task.Api/Task.Api.csproj, Task.slnx, Task/Program.cs, Task/ITaskService.cs moved, various using statements]
---

# Phase 1 Plan 1: Prepare Shared Components Summary

Created Task.Core shared library with unified TaskItem model, ITaskService interface, DTOs, and SQLite database logic for CLI and API reuse.

## Objective

Prepare Task.Core as a shared .NET class library containing unified components for both CLI and API projects.

## Tasks Completed

| Task | Name | Status | Commit |
| ---- | ---- | ------ | ------ |
| 1 | Create Task.Core class library | ✅ | 0a248f5 |
| 2 | Move unified TaskItem model | ✅ | 9856e78 |
| 3 | Extract ITaskService, DTOs, Database logic | ✅ | 9856e78 |
| 4 | Include SCHEMA.sql and migration scripts | ✅ | 9856e78 |
| 5 | Package vector extensions and utilities | ✅ | 9856e78 |
| 6 | Publish as NuGet package | ✅ | n/a |

## Decisions Made

- **Status Standardization**: Unified task status to "todo", "in_progress", "done" across CLI and API
- **DependsOn Inclusion**: Included DependsOn in shared TaskItem as it exists in CLI and is valuable for task dependencies
- **DTO Unification**: Consolidated DTOs in Task.Core with JSON converters for consistent API communication
- **Packaging Choice**: Created NuGet package for distribution, with project references for local development
- **Namespace Changes**: Updated all namespaces to Task.Core for shared components

## Tech Stack Updates

- **Added**: Microsoft.Data.Sqlite for database operations in shared library
- **Patterns Established**: Repository pattern via Database class implementing ITaskService

## File Changes

- **Created**: 11 new files in Task.Core/ including core models, interfaces, and database logic
- **Modified**: Updated project references and using statements across CLI and API
- **Moved**: Consolidated shared code from CLI and API into Task.Core

## Deviations from Plan

None - plan executed exactly as written.

## Authentication Gates

None encountered during execution.

## Performance Metrics

- **Duration**: 00:12:00
- **Completed**: 2026-02-22