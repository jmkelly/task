# Test Plan for Task

This outlines the testing strategy using xUnit for unit tests and Spectre.Console.Testing for integration tests. Aim for high coverage on core logic and CLI workflows.

## Testing Framework Setup
- Use xUnit for test discovery and execution.
- Mock dependencies (e.g., database) using Moq or in-memory SQLite.
- Run tests with `dotnet test`.

## Unit Tests (Tests/UnitTests/)

### DatabaseService Tests
- **CRUD Operations**: Test add, get, update, delete tasks with valid/invalid inputs, including 6-character Task UID generation uniqueness, following formatting rules: uppercase letters (A-Z, no I, O, L), digits 2-9 only, length = 6, and uniqueness by DB constraint.
- **Transactions**: Ensure atomicity for multi-step operations.
- **Edge Cases**: Null values, duplicate UIDs (should fail UNIQUE constraint for 6-character task IDs) (should fail UNIQUE constraint), constraint violations.

### SearchService Tests
- **FTS Queries**: Test full-text search on title, description, tags with exact/partial matches.
- **Semantic Search**: Mock embeddings and test vector similarity queries.
- **Hybrid Search**: Combine FTS and semantic results correctly.
- **Performance**: Benchmark search on 1000+ tasks (simulate large dataset).

### EmbeddingService Tests
- **Embedding Generation**: Test text-to-vector conversion with known inputs.
- **Caching**: Ensure embeddings are cached to avoid redundant computations.
- **Error Handling**: Handle embedding service failures gracefully.

### OutputService Tests
- **Rich Rendering**: Test table generation, colors for priority/status.
- **JSON Output**: Verify structured JSON for tasks and errors.
- **Plain Text**: Test minimal output for scripting.

### Model Validation Tests
- **Task Model**: Test property constraints (e.g., priority enum, date format).
- **DTOs**: Ensure serialization/deserialization matches expectations.

## Integration Tests (Tests/IntegrationTests/)

### CLI Command Workflows
- **Add-List Cycle**: Add task via CLI, verify in list output.
- **Search End-to-End**: Add tasks, perform searches, check results.
- **Import**: Import JSON/CSV, verify data integrity.
- **Interactive Mode**: Simulate user input for guided prompts (use Spectre.TestConsole).

### Error Scenarios
- **Invalid Inputs**: Test malformed commands, missing required fields.
- **Database Errors**: Simulate connection failures, disk full.
- **Exit Codes**: Verify correct exit codes (0=success, 1=error, 2=invalid input).

## Performance Tests
- **Load Testing**: Add 10,000 tasks, measure CRUD/search times.
- **Memory Usage**: Monitor for leaks during long-running operations.
- **Concurrent Access**: Test multiple CLI instances accessing the DB.

## Coverage Goals
- Unit tests: 80%+ code coverage.
- Integration tests: Cover all CLI commands and major workflows.
- Run tests on CI/CD pipeline (e.g., GitHub Actions) for .NET builds.

## Test Data
Use a test database with predefined tasks for consistent results. Include edge cases like tasks with special characters, long descriptions, future/past due dates.
