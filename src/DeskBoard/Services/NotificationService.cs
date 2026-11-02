using System;
using System.Windows;

namespace DeskBoard.Services;

public class NotificationService
{
    private readonly SettingsService _settingsService;

    public NotificationService(SettingsService settingsService)
    {
        _settingsService = settingsService;
    }

    public void ShowNotification(string title, string message)
    {
        // Disabled - no popups on desktop widget
        var settings = _settingsService.GetSettings();
        if (!settings.ShowNotifications)
            return;

        // Silent notification - can add toast later
    }

    public void ShowTaskCompleted(string taskTitle)
    {
        // Silent - no popup
    }

    public void ShowTaskCreated(string taskTitle)
    {
        // Silent - no popup
    }
}
