namespace DeskBoard.Models;

public class AppSettings
{
    public bool StartWithWindows { get; set; } = true;
    public bool HasExplicitlySetStartup { get; set; } = false;
    public bool AlwaysOnTop { get; set; } = false;
    public bool ShowNotifications { get; set; } = true;
    public double WindowLeft { get; set; } = -1;
    public double WindowTop { get; set; } = -1;
    public double WindowWidth { get; set; } = 300;
    public bool AutoBackup { get; set; } = true;
    public int AutoBackupInterval { get; set; } = 7; // days
}
