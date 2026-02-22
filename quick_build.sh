#!/bin/bash

# Build script for Task CLI - cross-platform self-contained binaries

set -e

PROJECT_DIR="Task"
OUTPUT_DIR="binaries"

# Clean previous builds
rm -rf "$OUTPUT_DIR"
mkdir -p "$OUTPUT_DIR"

echo "Building Task CLI for multiple platforms..."

# Linux x64
echo "Building for Linux x64..."
dotnet publish "$PROJECT_DIR" -c Release -r linux-x64 --self-contained -p:PublishSingleFile=true -o "$OUTPUT_DIR/task-linux-x64"

#copy this to /usr/local/bin task overwriting any existing
cp "$OUTPUT_DIR/task-linux-x64/Task" /usr/local/bin/task 

echo "Build complete! Self-contained directories are in the '$OUTPUT_DIR' directory:"
ls -la "$OUTPUT_DIR"
