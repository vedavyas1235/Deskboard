# DeskBoard Development Notes

## Build and Run Instructions

### Quick Start (Visual Studio)

1. Open `DeskBoard.sln` in Visual Studio 2022 or later
2. Ensure the build configuration is set to `Debug | x64` or `Release | x64`
3. Press `F5` to build and run the application
4. The application will launch as a native Windows desktop application

### Command Line Build

From the project root directory:

```bash
# Restore dependencies
dotnet restore

# Build
dotnet build src/DeskBoard/DeskBoard.csproj -c Release

# Run
dotnet run --project src/DeskBoard/DeskBoard.csproj -c Release

# Run tests
dotnet test
```

## Development Testing Checklist

- [ ] Application starts successfully from Visual Studio
- [ ] Tasks can be created, edited, and deleted
- [ ] Tasks persist after application restart
- [ ] Recurring tasks generate correctly
- [ ] Settings are saved and restored
- [ ] Backup creation works
- [ ] Windows startup registration works
- [ ] Desktop layer integration (requires manual testing)
- [ ] System tray minimization
- [ ] All unit tests pass
- [ ] All integration tests pass

## Publishing for Distribution

### Step 1: Build Release Version

Run the build script:

```bash
cd scripts
build.bat
```

Or manually:

```bash
dotnet publish src/DeskBoard/DeskBoard.csproj -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true -o ./publish
```

### Step 2: Test the Published Executable

1. Navigate to `publish` folder
2. Run `DeskBoard.exe`
3. Verify all functionality works
4. Check that database is created in `%LOCALAPPDATA%\DeskBoard`

### Step 3: Create Installer

1. Download and install Inno Setup from https://jrsoftware.org/isdl.php
2. Open `installer/DeskBoard.iss` in Inno Setup Compiler
3. Click "Build" > "Compile"
4. The installer `DeskBoard-Setup.exe` will be created in the `publish` folder

### Step 4: Test Installer

1. Run `DeskBoard-Setup.exe`
2. Install to a test location
3. Verify Start Menu shortcuts
4. Test the installed application
5. Test uninstallation (check data preservation prompt)

## Known Development Issues

### .NET 10 Compatibility

The project uses .NET 10 which may not be released yet. If you encounter build errors:

1. Update `TargetFramework` in all `.csproj` files to `net9.0-windows` or `net8.0-windows`
2. Update package versions as needed

Example fix:
```xml
<TargetFramework>net8.0-windows</TargetFramework>
```

### Desktop Layer Integration

The desktop layer service uses Windows API calls that may behave differently across Windows versions. Test on:
- Windows 10 (21H2 and later)
- Windows 11

### SQLite on Self-Contained Builds

The self-contained build includes native SQLite binaries. If you encounter database errors:
- Ensure `IncludeNativeLibrariesForSelfExtract` is set to `true`
- Check that `Microsoft.Data.Sqlite` package is up to date

## Code Architecture Notes

### Dependency Injection

Services are registered in `App.xaml.cs`:

```csharp
services.AddSingleton<DatabaseService>();
services.AddSingleton<TaskService>();
// etc...
```

### Database Initialization

The database is automatically created in `%LOCALAPPDATA%\DeskBoard\deskboard.db` on first run.

### Recurring Task Generation

The `RecurringTaskService` runs every 5 minutes to generate recurring task occurrences. The logic handles:
- Daily recurrence
- Weekly recurrence
- Monthly recurrence (with end-of-month edge cases)
- Yearly recurrence (with leap year handling)

### MVVM Pattern

- **Models**: `TaskItem`, `AppSettings`
- **ViewModels**: `MainViewModel`, `TaskItemViewModel` (use CommunityToolkit.Mvvm)
- **Views**: `MainWindow`, `SettingsWindow`

## Testing Strategy

### Unit Tests

Located in `tests/DeskBoard.Tests/`:
- `TaskServiceTests.cs`: Test CRUD operations
- `RecurringTaskTests.cs`: Test recurrence calculations

### Integration Tests

Located in `tests/DeskBoard.IntegrationTests/`:
- `DatabaseIntegrationTests.cs`: Test database creation and persistence

### Manual Testing Required

Some features require manual testing on Windows:
- Desktop layer integration
- Windows startup registration
- System tray behavior
- Windows Explorer restart recovery
- Toast notifications

## Maintenance

### Adding New Features

1. Add model classes in `Models/`
2. Create or update services in `Services/`
3. Create ViewModels in `ViewModels/`
4. Add views in `Views/`
5. Register services in `App.xaml.cs`
6. Write tests
7. Update README

### Database Migrations

Currently using a simple schema without migrations. For future versions:
1. Add version tracking to Settings table
2. Create migration scripts
3. Apply migrations on startup

## Performance Considerations

- Database queries use indices on `DisplayOrder`, `IsCompleted`, and `ParentRecurringTaskId`
- Recurring task generation runs every 5 minutes (configurable)
- Backup cleanup retains last 10 backups

## Security Notes

- Database stored in user's local AppData (not shared)
- No network communication
- Registry writes limited to HKEY_CURRENT_USER (no admin required)
- All file operations in user-writable locations
