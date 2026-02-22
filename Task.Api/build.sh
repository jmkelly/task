#!/bin/bash

# Build script for Task.Api - standalone build

set -e

echo "Building Task.Api..."

# Restore and build
dotnet restore Task.Api.csproj
dotnet build Task.Api.csproj -c Release

echo "Build complete."