#!/bin/bash

# Build script for Task CLI - cross-platform self-contained binaries

set -e

PROJECT_DIR="Task.Cli"
OUTPUT_DIR="binaries"

# Clean previous builds
rm -rf "$OUTPUT_DIR"
mkdir -p "$OUTPUT_DIR"

# Increment build number in .csproj file
CSProj="Task.Cli/Task.Cli.csproj"
CURR_VERSION=$(grep '<Version>' "$CSProj" | sed -E 's/.*<Version>([0-9]+\.[0-9]+\.[0-9]+\.([0-9]+))<\/Version>.*/\1/')
BUILD_NUM=$(echo "$CURR_VERSION" | awk -F. '{print $4}')
if [ -z "$BUILD_NUM" ]; then BUILD_NUM=0; fi
NEXT_BUILD_NUM=$((BUILD_NUM + 1))
NEW_VERSION=$(echo "$CURR_VERSION" | awk -F. -v nb="$NEXT_BUILD_NUM" '{print $1"."$2"."$3"."nb}')
sed -i -E "s/<Version>[0-9]+\.[0-9]+\.[0-9]+\.([0-9]+)<\/Version>/<Version>${NEW_VERSION}<\/Version>/" "$CSProj"
echo "Incremented build number: $CURR_VERSION → $NEW_VERSION"

echo "Building Task CLI for multiple platforms..."

# Linux x64
echo "Building for Linux x64..."
dotnet publish "$PROJECT_DIR" -c Release -r linux-x64 --self-contained -p:PublishSingleFile=true -o "$OUTPUT_DIR/task-linux-x64"

#copy this to /usr/local/bin task overwriting any existing
cp "$OUTPUT_DIR/task-linux-x64/Task.Cli" /usr/local/bin/task 

echo "Build complete! Self-contained directories are in the '$OUTPUT_DIR' directory:"
ls -la "$OUTPUT_DIR"
