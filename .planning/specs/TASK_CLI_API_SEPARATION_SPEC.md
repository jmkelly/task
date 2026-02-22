# Task.Cli API Separation Spec

## Overview
This spec outlines the steps to rename the current "Task" project to "Task.Cli" while creating a clean separation between the CLI and API components. The current repository contains both CLI and API code with some shared assets and duplicated models, requiring refactoring for proper decoupling into independent repositories.

## Current State Analysis
- **Repository Structure**: Single repository containing CLI (`Task/`), API (`Task.Api/`), tests, build scripts, and shared assets (schema, binaries, extensions).
- **Coupling Issues**:
  - Duplicated `TaskItem` models with inconsistencies (CLI has `DependsOn`, different status defaults).
  - Shared `SCHEMA.sql` and database logic in API only.
  - CLI has `--db` option for local DB but implementation is missing.
  - Tests and build scripts reference both components.
- **Separation Goal**: Split into two independent repositories (`Task.Cli` and `Task.Api`) with shared common library (`Task.Core`) to eliminate duplication and ensure clean boundaries.

## Proposed Architecture
1. **Task.Core** (Shared library): Common models, interfaces, DTOs, and database utilities.
2. **Task.Cli** (Renamed from current "Task" repo): Pure CLI application with local DB support.
3. **Task.Api** (New repo): Standalone web API with its own database handling.

## Detailed Implementation Plan

### Phase 1: Prepare Shared Components (Task.Core)
- Create a new .NET class library project for shared code.
- Move unified `TaskItem` model (resolve CLI/API differences: add `DependsOn` to API, standardize status defaults to "todo"/"in_progress"/"done").
- Extract `ITaskService` interface, DTOs (`TaskDto`, `TaskCreateDto`, etc.), and `Database.cs` logic.
- Include `SCHEMA.sql` and migration scripts.
- Package vector extensions and common utilities.
- Publish as NuGet package or Git submodule for both repos to reference.

### Phase 2: Separate Task.Api Repository
- Create new repository "Task.Api".
- Move `Task.Api/` directory contents to the new repo.
- Update to reference `Task.Core` for shared components.
- Implement missing features (e.g., `DependsOn` support if required by CLI integration).
- Update build scripts, README, and Docker setup for standalone deployment.
- Add API-specific tests and remove CLI dependencies.

### Phase 3: Rename and Refactor Task.Cli Repository
- Rename current repository from "Task" to "Task.Cli".
- Rename `Task/` directory to `Task.Cli/`.
- Update namespaces from `TaskApp` to `Task.Cli` throughout all code files.
- Set `AssemblyName` to "Task.Cli" in the csproj file.
- Implement local DB support using shared `Database.cs` (add `LocalTaskService` class implementing `ITaskService`).
- Update `ApiClient.cs` to use shared DTOs instead of duplicating them.
- Update solution file (`Task.slnx`) to reference `Task.Cli.csproj`.
- Update build scripts, README, and CI/CD configurations to reflect the new name.
- Modify tests to focus on CLI-specific functionality and remove API dependencies.

### Phase 4: Update Dependencies and Integration
- Both `Task.Cli` and `Task.Api` repos reference `Task.Core`.
- Ensure CLI maintains `--api-url` option for API integration mode.
- Update any cross-repo references in documentation or scripts.
- Verify Docker Compose and deployment scripts work independently for each component.

### Phase 5: Testing and Validation
- Run full test suites in both repositories.
- Validate CLI works correctly in both local DB mode and API integration mode.
- Test API standalone deployment and endpoints.
- Update documentation, examples, and installation instructions.
- Perform integration testing between CLI and API.

## Risks and Considerations
- **Breaking Changes**: Model unification and API endpoint updates may require user migration.
- **Feature Gaps**: CLI local DB support needs implementation (currently only API mode works).
- **Dependency Management**: Align .NET versions (net10.0) and NuGet package versions across repos.
- **Migration Path**: Existing users need guidance on migrating data and configurations.
- **Shared Assets**: Extensions local to projects; binaries in dedicated directory.

## Decisions Made
1. Use a shared `Task.Core` library to avoid duplication between repositories.
2. Rename the CLI directory to `Task.Cli/`.
3. Ensure `DependsOn` feature and status defaults ("todo"/"in_progress"/"done") are consistent between CLI and API.
4. Extensions will be kept local to their respective projects. Binaries will be in their own directory (shared reference).

## Success Criteria
- Clean separation with no shared dependencies between CLI and API repos.
- Both components build and run independently.
- CLI supports local DB mode and API integration mode.
- API deploys standalone with Docker.
- All existing functionality preserved with improved maintainability.
- Documentation updated for new repository structure.

This spec ensures clean separation while maintaining compatibility and preparing for future independent development of CLI and API components.