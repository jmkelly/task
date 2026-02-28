# Contributing

Thanks for contributing to the Task Management System! This repository includes a CLI client and REST API backend written in .NET/C#.

## Development setup

Prerequisites:

- .NET SDK 8.0+
- Docker (optional, for running the API via Compose)

Clone the repo:

```bash
git clone <repository-url>
cd Task
```

## Build

If you are building from the repo root, use one of the following options:

### CLI build (multi-platform publish)

```bash
./build.sh
```

### API build

```bash
./Task.Api/build.sh
```

### Direct dotnet build (update if your project layout changes)

```bash
dotnet build Task.Cli/Task.Cli.csproj -c Release
dotnet build Task.Api/Task.Api.csproj -c Release
```

## Test

Run the test projects directly (update if your project layout changes):

```bash
dotnet test Tests/Task.Tests.csproj
dotnet test Task.Api.Tests/Task.Api.Tests.csproj
```

If you add new test projects, make sure they are included in the test instructions above.

## Code style

- Keep changes small and focused by feature or bug.
- Prefer straightforward, explicit code over abstractions.
- Follow existing patterns in each project (CLI, API, Core).
- Add/adjust tests when you change behavior.

If you want automated formatting or analyzers, please update this section to match the repo’s tooling.

## PR process

1. Create a focused branch and keep commits scoped to a single purpose.
2. Ensure build and tests pass for the area you changed.
3. Describe the “why” and “what” in the PR summary.
4. Link any relevant issues or discussions.

## Code of Conduct

Please read and follow the [Code of Conduct](CODE_OF_CONDUCT.md).

## License

By contributing, you agree that your contributions will be licensed under the project [LICENSE](LICENSE).
