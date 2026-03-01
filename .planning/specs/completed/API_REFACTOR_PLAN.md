# API Refactor Plan: Separate Data Storage from CLI

## 1. Current Architecture Summary

The Task CLI is a monolithic C# application built with .NET, utilizing Spectre.Console.Cli for command-line interface handling. Key components include:

- **Database Layer**: `Database.cs` provides direct SQLite database access using Microsoft.Data.Sqlite. It manages:
  - Task CRUD operations (Add, Update, Delete, Get)
  - Full-text search via FTS5 virtual tables
  - Semantic search preparation (vector embeddings with sqlite-vss extension)
  - Database initialization and schema management

- **Command Layer**: Individual command classes (e.g., `AddCommand.cs`, `ListCommand.cs`) inherit from `TaskCommandSettings` and directly instantiate `Database` objects to perform operations.

- **Data Model**: `TaskItem` class represents tasks with fields like id, uid, title, description, priority, due_date, tags, status, created_at, updated_at.

- **Storage**: Local SQLite database (`tasks.db`) with virtual tables for search capabilities.

The current architecture tightly couples data access with CLI commands, with all database operations embedded within the CLI application.

## 2. Proposed Architecture

Introduce a layered architecture separating concerns:

- **CLI Layer**: Thin client that communicates via HTTP with the API layer. Focuses solely on user interaction, input validation, and output formatting.

- **API Layer**: ASP.NET Core Web API service that exposes RESTful endpoints for task management. Encapsulates all business logic and database operations.

- **Data Layer**: SQLite database accessed only through the API layer. Retains current schema and search capabilities.

Architecture Diagram (ASCII):
```
┌─────────────────┐    HTTP    ┌─────────────────┐    SQL    ┌─────────────────┐
│   Task CLI      │────────────│   Task API      │───────────│   SQLite DB     │
│                 │            │  (Web API)      │           │                 │
│ - Commands      │            │ - Controllers   │           │ - tasks table   │
│ - User Input    │            │ - Business Logic│           │ - Database.cs   │
│ - Output        │            │ - Database.cs   │           │ - FTS tables    │
└─────────────────┘            └─────────────────┘           └─────────────────┘
```

## 3. Implementation Steps (Detailed, Phased Approach)

### Phase 1: API Foundation (2-3 weeks)
1. **Create new ASP.NET Core Web API project** (`Task.Api`)
   - Use .NET 10+ for consistency
   - Add necessary NuGet packages: Microsoft.Data.Sqlite, SQLitePCLRaw.bundle_green, etc.
   - Configure dependency injection for Database service

2. **Move and adapt Database.cs to API project**
   - Copy Database.cs to Task.Api
   - Update namespace to Task.Api
   - Ensure all async operations remain compatible

 3. **Create TaskController**
    - Implement REST endpoints:
      - `GET /api/tasks` - List all tasks (query params: status, priority, tags, due_before, due_after, limit, offset, sort_by, sort_order)
      - `GET /api/tasks/{uid}` - Get task by UID
      - `POST /api/tasks` - Create new task
      - `PUT /api/tasks/{uid}` - Update task
      - `DELETE /api/tasks/{uid}` - Delete task
      - `PATCH /api/tasks/{uid}/complete` - Mark task complete
      - `GET /api/tasks/search?q={query}&type={fts|semantic|hybrid}` - Search tasks
      - `POST /api/tasks/import?format=json|csv` - Import tasks
      - `GET /api/tags` - Get all unique tags

4. **Implement API data transfer objects (DTOs)**
   - Create TaskDto, TaskCreateDto, TaskUpdateDto
   - Handle serialization/deserialization of dates, tags lists

5. **Add API configuration**
   - CORS policy for CLI communication
   - JSON serialization settings
   - Error handling middleware

### Phase 2: CLI Refactor (1-2 weeks)
6. **Add HTTP client to CLI project**
   - Add System.Net.Http.Json package
   - Create ApiClient service class in CLI

7. **Refactor command classes**
   - Replace Database instantiation with ApiClient
   - Update all commands to use API endpoints instead of direct DB calls
   - Handle HTTP errors and map to appropriate CLI exit codes

8. **Update Program.cs**
   - Add configuration for API base URL
   - Modify GetDatabaseAsync to return ApiClient instead

### Phase 3: Enhanced Features (1 week)
 9. **Implement advanced search and import endpoints**
    - Enhanced search with type parameter (fts, semantic, hybrid)
    - Import tasks from JSON/CSV body

10. **Add API versioning and documentation**
    - Implement API versioning
    - Add Swagger/OpenAPI documentation

### Phase 4: Testing and Validation (1-2 weeks)
 11. **Update unit tests**
     - Modify Database tests to work with API
     - Add integration tests for API endpoints (including enhanced search, import)
     - Update CLI integration tests

12. **Performance testing**
    - Compare latency between direct DB and API calls
    - Optimize API endpoints if needed

## 4. Migration Strategy

4. **Configuration Management**
   - Add `--api-url` option to CLI commands
   - Environment variable for API base URL
   - Default to local API instance during transition

## 5. Benefits and Risks

### Benefits
- **Separation of Concerns**: Clear separation between UI, business logic, and data access
- **Scalability**: API can be scaled independently, support multiple clients
- **Maintainability**: Easier testing and modification of business logic
- **Extensibility**: Support for web/mobile clients, third-party integrations
- **Security**: Centralized access control, API authentication possible
- **Deployment Flexibility**: API can be containerized, deployed to cloud

### Risks
- **Performance Overhead**: HTTP round-trips vs. direct DB access
- **Network Dependency**: CLI requires network connectivity to API
- **Increased Complexity**: Additional service to deploy and maintain
- **Error Handling**: HTTP errors vs. direct exception handling
- **Data Consistency**: Potential for eventual consistency issues
- **Development Overhead**: Dual maintenance during transition

### Mitigation Strategies
- Optimize API performance with caching, connection pooling
- Support offline mode or local DB fallback
- Comprehensive error handling and retry logic
- Automated testing for both API and CLI
- Monitoring and logging for API service

## 6. Open Questions/Decisions

1. **Authentication**: Will the API require authentication? API key, JWT, or none initially? API key

2. **Hosting**: Where will the API be hosted? Local process, Docker container, cloud service? API hosted in docker container initially

3. **API Design**: REST vs GraphQL? Synchronous vs asynchronous operations? REST. Async.

4. **Data Serialization**: JSON with custom converters vs protocol buffers? JSON

5. **Versioning**: How to handle API versioning and backward compatibility? Version API, start with v1

6. **Offline Support**: Should CLI support offline mode with local DB sync? No

7. **Monitoring**: What logging and metrics to implement for the API?

8. **Deployment**: How to deploy API alongside CLI binaries? docker-compose

9. **Database Connection**: Single connection vs connection pooling in API? connection pooling

10. **CORS**: Which origins to allow for CORS in development/production?
