using System;
using System.Diagnostics;
using Microsoft.Win32;

namespace DeskBoard.DesktopIntegration;

public class StartupRegistryHelper
{
    private const string RunKey = @"SOFTWARE\Microsoft\Windows\CurrentVersion\Run";
    private const string AppName = "DeskBoard";

    public void AddToStartup()
    {
        try
        {
            using var key = Registry.CurrentUser.OpenSubKey(RunKey, writable: true);
            if (key == null)
                return;

            string? exePath = Process.GetCurrentProcess().MainModule?.FileName;
            
            // If running via dotnet host or development, point to the actual DeskBoard.exe
            if (string.IsNullOrEmpty(exePath) || exePath.EndsWith("dotnet.exe", StringComparison.OrdinalIgnoreCase))
            {
                string candidate = System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "DeskBoard.exe");
                if (System.IO.File.Exists(candidate))
                    exePath = candidate;
            }

            if (!string.IsNullOrEmpty(exePath))
            {
                key.SetValue(AppName, $"\"{exePath}\"");
            }
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Error adding to startup: {ex.Message}");
        }
    }

    public void RemoveFromStartup()
    {
        try
        {
            using var key = Registry.CurrentUser.OpenSubKey(RunKey, writable: true);
            if (key == null)
                return;

            if (key.GetValue(AppName) != null)
            {
                key.DeleteValue(AppName);
            }
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Error removing from startup: {ex.Message}");
        }
    }

    public bool IsInStartup()
    {
        try
        {
            using var key = Registry.CurrentUser.OpenSubKey(RunKey, writable: false);
            return key?.GetValue(AppName) != null;
        }
        catch
        {
            return false;
        }
    }
}
