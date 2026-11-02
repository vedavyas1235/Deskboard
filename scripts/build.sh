#!/bin/bash

# DeskBoard Build Script
# Builds the DeskBoard application in Release mode

set -e

echo "=========================================="
echo "  DeskBoard Build Script"
echo "=========================================="
echo ""

# Configuration
PROJECT_ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
PROJECT_FILE="$PROJECT_ROOT/src/DeskBoard/DeskBoard.csproj"
OUTPUT_DIR="$PROJECT_ROOT/publish"
CONFIGURATION="Release"
RUNTIME="win-x64"

echo "Project root: $PROJECT_ROOT"
echo "Output directory: $OUTPUT_DIR"
echo ""

# Clean previous build
echo "Cleaning previous build..."
if [ -d "$OUTPUT_DIR" ]; then
    rm -rf "$OUTPUT_DIR"
fi
mkdir -p "$OUTPUT_DIR"

# Restore dependencies
echo ""
echo "Restoring dependencies..."
dotnet restore "$PROJECT_FILE"

# Build the project
echo ""
echo "Building DeskBoard..."
dotnet build "$PROJECT_FILE" -c "$CONFIGURATION" --no-restore

# Run tests
echo ""
echo "Running tests..."
dotnet test "$PROJECT_ROOT/tests/DeskBoard.Tests/DeskBoard.Tests.csproj" --no-restore --verbosity normal
dotnet test "$PROJECT_ROOT/tests/DeskBoard.IntegrationTests/DeskBoard.IntegrationTests.csproj" --no-restore --verbosity normal

# Publish the application
echo ""
echo "Publishing DeskBoard..."
dotnet publish "$PROJECT_FILE" \
    -c "$CONFIGURATION" \
    -r "$RUNTIME" \
    --self-contained true \
    -p:PublishSingleFile=true \
    -p:IncludeNativeLibrariesForSelfExtract=true \
    -p:EnableCompressionInSingleFile=true \
    -o "$OUTPUT_DIR"

echo ""
echo "=========================================="
echo "  Build completed successfully!"
echo "=========================================="
echo ""
echo "Output location: $OUTPUT_DIR"
echo ""
echo "Files generated:"
ls -lh "$OUTPUT_DIR"
echo ""
echo "You can now:"
echo "  1. Run the application: $OUTPUT_DIR/DeskBoard.exe"
echo "  2. Create an installer using the installer script"
echo ""
