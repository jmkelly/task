# Task.Api

REST API backend for the Task Management System.

## Features

- RESTful endpoints for task management
- Scalar/OpenAPI documentation
- CORS support for web integrations
- Advanced filtering and sorting
- Tag management
- JSON serialization with custom converters

## Dependencies

This project depends on Task.Core for core logic and data access.

## Installation

### Running via Docker Compose (Recommended)

1. Ensure Docker and Docker Compose are installed.
2. From the Task.Api directory, run:
   ```bash
   docker-compose up -d
   ```
   (Assuming docker-compose.yml is set up for standalone)

### Building and Running from Source

Requirements:
- .NET 10.0 SDK

1. Navigate to the Task.Api directory.
2. Run:
   ```bash
   ./build.sh
   dotnet run
   ```

The API will start on http://localhost:5000 (development) or https://localhost:5001 (with SSL).

For production, configure ASPNETCORE_ENVIRONMENT and DatabasePath.

## Usage

- Scalar UI: http://localhost:8080/scalar
- API Base URL: http://localhost:8080/api

### Key Endpoints

- `GET /api/tasks` - List tasks
- `POST /api/tasks` - Create task
- etc. (see full list in main README)