using System;
using System.IO;
using Microsoft.Data.Sqlite;

namespace DeskBoard.Database;

public class DatabaseService
{
    private readonly string _connectionString;
    private readonly string _databasePath;

    public DatabaseService()
    {
        var appDataPath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "DeskBoard"
        );
        Directory.CreateDirectory(appDataPath);

        _databasePath = Path.Combine(appDataPath, "deskboard.db");
        _connectionString = $"Data Source={_databasePath}";
    }

    public string DatabasePath => _databasePath;

    public void Initialize()
    {
        using var connection = new SqliteConnection(_connectionString);
        connection.Open();

        var command = connection.CreateCommand();
        command.CommandText = @"
            CREATE TABLE IF NOT EXISTS Tasks (
                Id INTEGER PRIMARY KEY AUTOINCREMENT,
                Title TEXT NOT NULL,
                Description TEXT,
                CreatedAt TEXT NOT NULL,
                CompletedAt TEXT,
                IsCompleted INTEGER NOT NULL DEFAULT 0,
                DisplayOrder INTEGER NOT NULL DEFAULT 0,
                IsRecurring INTEGER NOT NULL DEFAULT 0,
                RecurrenceType INTEGER NOT NULL DEFAULT 0,
                RecurrenceInterval INTEGER NOT NULL DEFAULT 1,
                RecurrenceStartDate TEXT,
                RecurrenceEndDate TEXT,
                ParentRecurringTaskId INTEGER,
                OccurrenceDate TEXT
            );

            CREATE TABLE IF NOT EXISTS Settings (
                Key TEXT PRIMARY KEY,
                Value TEXT NOT NULL
            );

            CREATE INDEX IF NOT EXISTS idx_tasks_displayorder ON Tasks(DisplayOrder);
            CREATE INDEX IF NOT EXISTS idx_tasks_completed ON Tasks(IsCompleted);
            CREATE INDEX IF NOT EXISTS idx_tasks_parent ON Tasks(ParentRecurringTaskId);
        ";
        command.ExecuteNonQuery();
    }

    public SqliteConnection GetConnection()
    {
        var connection = new SqliteConnection(_connectionString);
        connection.Open();
        return connection;
    }

    public void ExecuteNonQuery(string commandText, Action<SqliteCommand>? configureParameters = null)
    {
        using var connection = GetConnection();
        using var command = connection.CreateCommand();
        command.CommandText = commandText;
        configureParameters?.Invoke(command);
        command.ExecuteNonQuery();
    }

    public T? ExecuteScalar<T>(string commandText, Action<SqliteCommand>? configureParameters = null)
    {
        using var connection = GetConnection();
        using var command = connection.CreateCommand();
        command.CommandText = commandText;
        configureParameters?.Invoke(command);
        var result = command.ExecuteScalar();
        return result == null ? default : (T)Convert.ChangeType(result, typeof(T));
    }
}
