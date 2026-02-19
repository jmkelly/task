# Dependencies for Task

## NuGet Packages

### Core Dependencies
- **Spectre.Console.Cli** (latest): Command-line interface framework for parsing commands and options.
- **Spectre.Console** (latest): Rich console output with tables, colors, and progress bars.
- **Microsoft.Data.Sqlite** (latest): SQLite database provider for .NET.
- **sqlite-vss** (latest): SQLite extension for vector similarity search using FAISS.

### Search and Embeddings
- **SentenceTransformers.NET** (latest): For generating text embeddings for semantic search (alternative: Microsoft.ML for custom models).

### Testing
- **xUnit** (latest): Unit testing framework.
- **Microsoft.NET.Test.Sdk** (latest): Test SDK for .NET.
- **Spectre.Console.Testing** (latest): For testing console output in unit tests.

### Optional/Development
- **Microsoft.Extensions.Configuration** (latest): For configuration management if needed.
- **Microsoft.Extensions.Logging** (latest): For logging if error handling expands.

## Installation Notes
Run `dotnet add package [package-name]` for each package in the .NET CLI project directory.

Ensure sqlite-vss is compatible with your platform (supports Windows, macOS, Linux).