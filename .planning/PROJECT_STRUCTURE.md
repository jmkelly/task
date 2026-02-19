# Project Structure for CLI Task Recording App

This outlines the recommended folder and file structure for the .NET C# project. Follow this to maintain modularity and separation of concerns.

## Root Directory
- **Task.csproj**: .NET project file with package references.
- **Program.cs**: Entry point for the CLI application.
- **appsettings.json**: Optional configuration file for settings like database path.

## Commands/ Directory
Contains CLI command implementations using Spectre.Console.Cli.
- **AddCommand.cs**: Handles 'add' command for creating tasks.
- **ListCommand.cs**: Handles 'list' command for displaying tasks.
- **EditCommand.cs**: Handles 'edit' command for updating tasks.
- **DeleteCommand.cs**: Handles 'delete' command for removing tasks.
- **CompleteCommand.cs**: Handles 'complete' command for marking tasks done.
- **SearchCommand.cs**: Handles 'search' command with FTS and semantic options.

## Models/ Directory
Data models and DTOs.
- **Task.cs**: Main Task model with properties (id, title, description, etc.).
- **TaskDto.cs**: Data transfer object for JSON input/output.
- **SearchResult.cs**: Model for search results.

## Services/ Directory
Business logic and data access layers.
- **DatabaseService.cs**: Handles SQLite connections, CRUD operations.
- **SearchService.cs**: Manages FTS and semantic search logic.
- **EmbeddingService.cs**: Generates and manages vector embeddings.
- **OutputService.cs**: Handles rendering output (rich tables, JSON, plain text).
- **IdGenerator.cs**: Generates unique, sortable 6-character base30 IDs (lowercase alphabet excluding confusing chars: 0,1,i,l,o) without global state.

## Exceptions/ Directory
Custom exceptions for error handling.
- **TaskException.cs**: Base exception for task-related errors.
- **ValidationException.cs**: For input validation errors.
- **DatabaseException.cs**: For database operation failures.

## Tests/ Directory
Unit and integration tests.
- **UnitTests/**: xUnit tests for services and models.
- **IntegrationTests/**: Tests for full CLI workflows using Spectre.Console.Testing.

## Other Files
- **README.md**: Documentation with usage examples and setup instructions.
- **SCHEMA.sql**: Database schema definition.
- **DEPENDENCIES.md**: Package dependencies list.