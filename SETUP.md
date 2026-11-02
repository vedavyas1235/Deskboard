# DeskBoard - Setup and Build Guide

## Prerequisites Installation

### 1. Install .NET 8.0 SDK

Download and install the .NET 8.0 SDK from:
https://dotnet.microsoft.com/download/dotnet/8.0

Choose: **.NET 8.0 SDK (v8.0.x) - Windows x64 Installer**

After installation, verify by opening a new terminal and running:
```bash
dotnet --version
```

You should see output like: `8.0.x`

### 2. Install Visual Studio 2022 (Optional but Recommended)

Download Visual Studio 2022 Community Edition (free):
https://visualstudio.microsoft.com/downloads/

During installation, select these workloads:
- ✅ .NET desktop development
- ✅ Desktop development with C++

### 3. Install Inno Setup (For Creating Installer)

Download Inno Setup 6 from:
https://jrsoftware.org/isdl.php

Install with default options.

## Quick Start

### Option A: Using Visual Studio (Easiest)

1. Navigate to the DeskBoard folder
2. Double-click `DeskBoard.sln`
3. Visual Studio will open the solution
4. Press **F5** to build and run

The application will launch on your desktop!

### Option B: Using Command Line

Open PowerShell or Command Prompt and navigate to the DeskBoard folder:

```bash
# Navigate to project
cd "D:\Sonu\WORKSHOPS\Disk notes\DeskBoard"

# Restore dependencies
dotnet restore

# Build the project
dotnet build src/DeskBoard/DeskBoard.csproj -c Debug

# Run the application
dotnet run --project src/DeskBoard/DeskBoard.csproj -c Debug
```

## Building for Distribution

### Step 1: Build Release Version

```bash
cd "D:\Sonu\WORKSHOPS\Disk notes\DeskBoard"
cd scripts
build.bat
```

This will:
- Restore all dependencies
- Build the project in Release mode
- Run all tests
- Create a self-contained executable in the `publish` folder

### Step 2: Test the Executable

```bash
cd ..\publish
DeskBoard.exe
```

The application should launch successfully!

### Step 3: Create Installer

1. Open Inno Setup Compiler
2. Click **File** > **Open**
3. Navigate to: `D:\Sonu\WORKSHOPS\Disk notes\DeskBoard\installer\DeskBoard.iss`
4. Click **Build** > **Compile**
5. The installer `DeskBoard-Setup.exe` will be created in the `publish` folder

### Step 4: Create Portable Version

```bash
cd scripts
create-portable.bat
```

This creates `DeskBoard-Portable.zip` in the `publish` folder.

## Testing the Application

### Run Unit Tests

```bash
dotnet test tests/DeskBoard.Tests/DeskBoard.Tests.csproj --verbosity normal
```

### Run Integration Tests

```bash
dotnet test tests/DeskBoard.IntegrationTests/DeskBoard.IntegrationTests.csproj --verbosity normal
```

### Run All Tests

```bash
dotnet test
```

## Testing Checklist

After building, manually verify:

### Basic Functionality
- [ ] Application starts without errors
- [ ] Can create a new task
- [ ] Can edit a task title
- [ ] Can complete/uncomplete a task
- [ ] Can delete a task
- [ ] Tasks persist after closing and reopening

### Settings
- [ ] Open Settings window (⚙️ button)
- [ ] Change settings and save
- [ ] Settings persist after restart

### Recurring Tasks
- [ ] Create a recurring task
- [ ] Wait 5 minutes or restart app
- [ ] Check if recurring task instance is created

### Backup & Restore
- [ ] Click "Create Backup Now" in Settings
- [ ] Verify backup file is created in `%LOCALAPPDATA%\DeskBoard\Backups`

### Windows Integration
- [ ] Enable "Start with Windows" in Settings
- [ ] Check Windows Registry: `HKEY_CURRENT_USER\SOFTWARE\Microsoft\Windows\CurrentVersion\Run`
- [ ] Verify DeskBoard entry exists

## File Locations

After installation:

- **Application Data**: `%LOCALAPPDATA%\DeskBoard\`
  - Database: `deskboard.db`
  - Backups: `Backups\` subfolder

To view:
```bash
# Open in File Explorer
explorer "%LOCALAPPDATA%\DeskBoard"
```

## Troubleshooting

### "The target framework 'net8.0-windows' was not found"

**Solution**: Install .NET 8.0 SDK (see Prerequisites above)

### "MSBuild version is too low"

**Solution**: Update Visual Studio to 2022 or install .NET 8.0 SDK

### Build succeeds but app won't run

**Solution**: 
1. Check Windows Event Viewer for errors
2. Ensure all dependencies are included (use self-contained build)
3. Try running from command line to see error messages:
   ```bash
   cd publish
   DeskBoard.exe
   ```

### "Microsoft.Data.Sqlite.dll not found"

**Solution**: Rebuild using the self-contained publish command:
```bash
dotnet publish src/DeskBoard/DeskBoard.csproj -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true -o ./publish
```

### Desktop Layer Not Working

**Desktop integration** uses Windows API calls. This feature:
- Requires Windows Explorer to be running
- May not work in all Windows configurations
- Is optional (disable in Settings if issues occur)

To test:
1. Open Settings
2. Check "Show desktop layer"
3. Restart application
4. Tasks should appear on desktop (behind icons)

### Tests Fail

Some tests use reflection to test private methods. If tests fail:
1. Check that SQLite database can be created in temp directory
2. Verify file permissions
3. Run Visual Studio as Administrator (only if needed)

## Development Workflow

### Making Changes

1. Edit source files in `src/DeskBoard/`
2. Build: Press **F5** in Visual Studio or run `dotnet build`
3. Test your changes
4. Run tests: `dotnet test`
5. Create pull request or commit changes

### Adding New Features

See `DEVELOPMENT.md` for architecture details and contribution guidelines.

## Next Steps

Once you have the application built and running:

1. ✅ Verify all features work as expected
2. ✅ Run all tests
3. ✅ Test the installer
4. ✅ Create portable version
5. ✅ Test on a clean Windows VM (optional but recommended)
6. 📦 Distribute to users!

## Support

For issues or questions:
- Check `DEVELOPMENT.md` for detailed technical documentation
- Review the code comments in source files
- Check Windows Event Viewer for runtime errors
- Review SQLite database with a tool like [DB Browser for SQLite](https://sqlitebrowser.org/)

## Summary

**DeskBoard** is now ready to build! Once you install the .NET 8.0 SDK:

```bash
cd "D:\Sonu\WORKSHOPS\Disk notes\DeskBoard"
dotnet restore
dotnet build
dotnet run --project src/DeskBoard/DeskBoard.csproj
```

The application will launch as a native Windows desktop application with all features fully functional.
