@echo off
REM Create a portable ZIP distribution of DeskBoard

setlocal enabledelayedexpansion

echo ==========================================
echo   DeskBoard Portable Package Creator
echo ==========================================
echo.

set "PROJECT_ROOT=%~dp0.."
set "PUBLISH_DIR=%PROJECT_ROOT%\publish"
set "PORTABLE_DIR=%PROJECT_ROOT%\publish\DeskBoard-Portable"
set "ZIP_FILE=%PROJECT_ROOT%\publish\DeskBoard-Portable.zip"

REM Check if publish directory exists
if not exist "%PUBLISH_DIR%\DeskBoard.exe" (
    echo ERROR: Published application not found!
    echo Please run build.bat first.
    exit /b 1
)

REM Create portable directory
if exist "%PORTABLE_DIR%" (
    rmdir /s /q "%PORTABLE_DIR%"
)
mkdir "%PORTABLE_DIR%"

REM Copy files
echo Copying files to portable directory...
xcopy "%PUBLISH_DIR%\*.exe" "%PORTABLE_DIR%\" /Y
xcopy "%PUBLISH_DIR%\*.dll" "%PORTABLE_DIR%\" /Y
xcopy "%PUBLISH_DIR%\*.json" "%PORTABLE_DIR%\" /Y

REM Create README
echo Creating portable README...
(
echo DeskBoard - Portable Version
echo.
echo This is a portable version of DeskBoard.
echo.
echo USAGE:
echo 1. Extract this folder anywhere
echo 2. Run DeskBoard.exe
echo.
echo DATA STORAGE:
echo Tasks and settings are stored in:
echo %%LOCALAPPDATA%%\DeskBoard\
echo.
echo For more information, see the full README.md
) > "%PORTABLE_DIR%\README.txt"

REM Create ZIP (requires PowerShell)
echo Creating ZIP archive...
if exist "%ZIP_FILE%" del "%ZIP_FILE%"
powershell -command "Compress-Archive -Path '%PORTABLE_DIR%' -DestinationPath '%ZIP_FILE%' -CompressionLevel Optimal"

if exist "%ZIP_FILE%" (
    echo.
    echo ==========================================
    echo   Portable package created successfully!
    echo ==========================================
    echo.
    echo Location: %ZIP_FILE%
    echo.
) else (
    echo ERROR: Failed to create ZIP file
    exit /b 1
)

endlocal
