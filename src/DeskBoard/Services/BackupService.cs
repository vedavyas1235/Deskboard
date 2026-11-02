using System;
using System.IO;
using System.Linq;
using DeskBoard.Database;

namespace DeskBoard.Services;

public class BackupService
{
    private readonly DatabaseService _databaseService;
    private readonly string _backupDirectory;

    public BackupService(DatabaseService databaseService)
    {
        _databaseService = databaseService;

        _backupDirectory = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "DeskBoard",
            "Backups"
        );

        Directory.CreateDirectory(_backupDirectory);
    }

    public string CreateBackup()
    {
        var timestamp = DateTime.Now.ToString("yyyyMMdd_HHmmss");
        var backupFileName = $"deskboard_backup_{timestamp}.db";
        var backupPath = Path.Combine(_backupDirectory, backupFileName);

        File.Copy(_databaseService.DatabasePath, backupPath, overwrite: true);

        // Keep only last 10 backups
        CleanOldBackups(10);

        return backupPath;
    }

    public void RestoreBackup(string backupPath)
    {
        if (!File.Exists(backupPath))
            throw new FileNotFoundException("Backup file not found", backupPath);

        // Create a backup of current state before restoring
        CreateBackup();

        File.Copy(backupPath, _databaseService.DatabasePath, overwrite: true);
    }

    public string[] GetAvailableBackups()
    {
        return Directory.GetFiles(_backupDirectory, "deskboard_backup_*.db")
            .OrderByDescending(f => f)
            .ToArray();
    }

    private void CleanOldBackups(int keepCount)
    {
        var backups = GetAvailableBackups();

        if (backups.Length <= keepCount)
            return;

        foreach (var backup in backups.Skip(keepCount))
        {
            try
            {
                File.Delete(backup);
            }
            catch
            {
                // Ignore errors when deleting old backups
            }
        }
    }
}
