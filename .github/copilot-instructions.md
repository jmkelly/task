# Copilot Instructions for Task Management System

This file provides essential guidance for Copilot and other AI assistants to work effectively in this repository. It covers build, test, and lint commands, high-level architecture, and key conventions specific to this codebase.

---

## Build, Test, and Lint Commands

### Build
- **Full cross-platform CLI build:**
  ```bash
  ./build.sh
  ```
  - Produces self-contained binaries for Linux, Windows, and macOS in `binaries/`.
- **Quick local CLI build:**
  ```bash
  ./quick_build.sh
  ```
  - Builds Linux CLI and copies to `/usr/local/bin/task`.
- **API build:**
  ```bash
  cd Task.Api
  ./build.sh
  ```
  or from root:
  ```bash
  dotnet build Task.Api/Task.Api.csproj -c Release
  ```
- **Solution build (all projects):**
  ```bash
  dotnet build Task.slnx -c Release
  ```

### Test
- **Run all tests:**
  ```bash
  dotnet test Task.slnx
  ```
- **Run a single test (by filter):**
  ```bash
  dotnet test Task.slnx --filter FullyQualifiedName~ErrorHelperTests.ValidatePriority_WithInvalidPriority_ReturnsFalse
  ```
- **API integration tests:**
  ```bash
  dotnet test Task.Api.Tests/Task.Api.Tests.csproj
  ```

### Lint
- No dedicated lint script; use `dotnet format` for code style:
  ```bash
  dotnet format Task.slnx
  ```

### Docker Compose (API backend)
- **Start API backend:**
  ```bash
  docker-compose up -d
  ```
- **Stop API backend:**
  ```bash
  docker-compose down
  ```

---

## High-Level Architecture

- **CLI Client (`Task.Cli/`):** Command-line interface for managing tasks locally or via API.
- **REST API Backend (`Task.Api/`):** Web API server for task management, with OpenAPI docs and CORS support.
- **Core Logic (`Task.Core/`):** Shared logic, data models, and database schema (SQLite with FTS5 and vector search).
- **Tests:**
  - `Task.Api.Tests/`: API integration tests
  - `Tests/`: CLI and core logic unit tests
- **Database:** SQLite, with FTS5 for full-text search and VSS for semantic search (requires `sqlite-vss` extension).
- **Docker:** API backend can be run in a container with persistent volume for database.

---

## Key Conventions

- **Task Status:** Only `todo`, `in_progress`, and `done` are valid status values.
- **Task Priority:** Only `high`, `medium`, and `low` are valid priorities.
- **Date Format:** Dates must be in ISO 8601 format (e.g., `2024-12-31`).
- **Tags:** Stored as comma-separated strings in the database.
- **API URLs:**
  - Local: `http://localhost:8080/api` (Docker) or `http://localhost:5000/api` (dev)
  - Swagger UI: `/swagger`
- **CLI Modes:**
  - Local DB (default) or remote API (`--api-url`)
- **Database Schema:** See `Task.Core/SCHEMA.sql` for authoritative schema and triggers.
- **Vector Search:** Requires `sqlite-vss` extension and compatible `.so` files in relevant directories.

---

## Integration with Other AI Assistants

- No other AI assistant config files detected (Claude, Cursor, Codex, Windsurf, Aider, Cline, etc.).

---

## Additional Notes

- Stress testing scripts are in `stress-test/` (see its README for usage).
- All build/test commands assume .NET 8.0 SDK is installed.
- For custom database paths, use CLI `--db <path>` or set `DatabasePath` in API environment.

---

_End of Copilot instructions._
