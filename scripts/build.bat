@echo off
REM DeskBoard Build Script for Windows
REM Builds the DeskBoard application in Release mode

setlocal enabledelayedexpansion

echo ==========================================
echo   DeskBoard Build Script
echo ==========================================
echo.

REM Configuration
set "PROJECT_ROOT=%~dp0.."
set "PROJECT_FILE=%PROJECT_ROOT%\src\DeskBoard\DeskBoard.csproj"
set "OUTPUT_DIR=%PROJECT_ROOT%\publish"
set "CONFIGURATION=Release"
set "RUNTIME=win-x64"

echo Project root: %PROJECT_ROOT%
echo Output directory: %OUTPUT_DIR%
echo.

REM Clean previous build
echo Cleaning previous build...
if exist "%OUTPUT_DIR%" (
    rmdir /s /q "%OUTPUT_DIR%"
)
mkdir "%OUTPUT_DIR%"

REM Restore dependencies
echo.
echo Restoring dependencies...
dotnet restore "%PROJECT_FILE%"
if errorlevel 1 (
    echo ERROR: Failed to restore dependencies
    exit /b 1
)

REM Build the project
echo.
echo Building DeskBoard...
dotnet build "%PROJECT_FILE%" -c "%CONFIGURATION%" --no-restore
if errorlevel 1 (
    echo ERROR: Build failed
    exit /b 1
)

REM Run tests
echo.
echo Running tests...
dotnet test "%PROJECT_ROOT%\tests\DeskBoard.Tests\DeskBoard.Tests.csproj" --no-restore --verbosity normal
if errorlevel 1 (
    echo WARNING: Some tests failed
)

dotnet test "%PROJECT_ROOT%\tests\DeskBoard.IntegrationTests\DeskBoard.IntegrationTests.csproj" --no-restore --verbosity normal
if errorlevel 1 (
    echo WARNING: Some integration tests failed
)

REM Publish the application
echo.
echo Publishing DeskBoard...
dotnet publish "%PROJECT_FILE%" -c "%CONFIGURATION%" -r "%RUNTIME%" --self-contained true -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true -p:EnableCompressionInSingleFile=true -o "%OUTPUT_DIR%"
if errorlevel 1 (
    echo ERROR: Publish failed
    exit /b 1
)

echo.
echo ==========================================
echo   Build completed successfully!
echo ==========================================
echo.
echo Output location: %OUTPUT_DIR%
echo.
echo You can now:
echo   1. Run the application: %OUTPUT_DIR%\DeskBoard.exe
echo   2. Create an installer using Inno Setup
echo.

endlocal
