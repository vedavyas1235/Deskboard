using System;
using System.Text.Json;
using DeskBoard.Database;
using DeskBoard.Models;

namespace DeskBoard.Services;

public class SettingsService
{
    private readonly DatabaseService _databaseService;
    private AppSettings? _cachedSettings;

    public SettingsService(DatabaseService databaseService)
    {
        _databaseService = databaseService;
    }

    public AppSettings GetSettings()
    {
        if (_cachedSettings != null)
            return _cachedSettings;

        var settingsJson = _databaseService.ExecuteScalar<string>(
            "SELECT Value FROM Settings WHERE Key = 'AppSettings'"
        );

        if (string.IsNullOrEmpty(settingsJson))
        {
            _cachedSettings = new AppSettings { StartWithWindows = true, HasExplicitlySetStartup = false };
            SaveSettings(_cachedSettings);
            new DesktopIntegration.StartupRegistryHelper().AddToStartup();
        }
        else
        {
            _cachedSettings = JsonSerializer.Deserialize<AppSettings>(settingsJson) ?? new AppSettings();
            
            // If startup was never explicitly customized by the user, ensure it defaults to true
            if (!_cachedSettings.HasExplicitlySetStartup)
            {
                _cachedSettings.StartWithWindows = true;
                SaveSettings(_cachedSettings);
                new DesktopIntegration.StartupRegistryHelper().AddToStartup();
            }
        }

        return _cachedSettings;
    }

    public void SaveSettings(AppSettings settings)
    {
        _cachedSettings = settings;
        var settingsJson = JsonSerializer.Serialize(settings);

        _databaseService.ExecuteNonQuery(
            @"INSERT OR REPLACE INTO Settings (Key, Value) VALUES ('AppSettings', @value)",
            cmd => cmd.Parameters.AddWithValue("@value", settingsJson)
        );
    }

    public void UpdateStartWithWindows(bool enabled)
    {
        var settings = GetSettings();
        settings.StartWithWindows = enabled;
        settings.HasExplicitlySetStartup = true;
        SaveSettings(settings);

        // Update Windows startup registry
        var registryHelper = new DesktopIntegration.StartupRegistryHelper();
        if (enabled)
            registryHelper.AddToStartup();
        else
            registryHelper.RemoveFromStartup();
    }
}
