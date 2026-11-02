using System;
using System.IO;
using DeskBoard.Database;
using Xunit;

namespace DeskBoard.IntegrationTests;

public class DatabaseIntegrationTests : IDisposable
{
    private readonly string _testDbPath;
    private readonly DatabaseService _databaseService;

    public DatabaseIntegrationTests()
    {
        _testDbPath = Path.Combine(Path.GetTempPath(), $"integration_test_{Guid.NewGuid()}.db");
        _databaseService = new DatabaseService();

        // Replace database path with test path
        var field = typeof(DatabaseService).GetField("_databasePath",
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        field?.SetValue(_databaseService, _testDbPath);

        _databaseService.Initialize();
    }

    [Fact]
    public void Database_ShouldCreateAllTables()
    {
        // Act
        using var connection = _databaseService.GetConnection();
        using var command = connection.CreateCommand();
        command.CommandText = @"
            SELECT name FROM sqlite_master
            WHERE type='table'
            ORDER BY name;
        ";

        var tables = new System.Collections.Generic.List<string>();
        using var reader = command.ExecuteReader();
        while (reader.Read())
        {
            tables.Add(reader.GetString(0));
        }

        // Assert
        Assert.Contains("Tasks", tables);
        Assert.Contains("Settings", tables);
    }

    [Fact]
    public void Database_ShouldCreateIndices()
    {
        // Act
        using var connection = _databaseService.GetConnection();
        using var command = connection.CreateCommand();
        command.CommandText = @"
            SELECT name FROM sqlite_master
            WHERE type='index' AND name NOT LIKE 'sqlite_%'
            ORDER BY name;
        ";

        var indices = new System.Collections.Generic.List<string>();
        using var reader = command.ExecuteReader();
        while (reader.Read())
        {
            indices.Add(reader.GetString(0));
        }

        // Assert
        Assert.Contains("idx_tasks_displayorder", indices);
        Assert.Contains("idx_tasks_completed", indices);
        Assert.Contains("idx_tasks_parent", indices);
    }

    [Fact]
    public void Database_ShouldPersistData()
    {
        // Arrange
        _databaseService.ExecuteNonQuery(
            "INSERT INTO Tasks (Title, CreatedAt, IsCompleted, DisplayOrder) VALUES (@title, @createdAt, 0, 1)",
            cmd =>
            {
                cmd.Parameters.AddWithValue("@title", "Test Task");
                cmd.Parameters.AddWithValue("@createdAt", DateTime.UtcNow.ToString("O"));
            }
        );

        // Act
        var title = _databaseService.ExecuteScalar<string>(
            "SELECT Title FROM Tasks WHERE DisplayOrder = 1"
        );

        // Assert
        Assert.Equal("Test Task", title);
    }

    public void Dispose()
    {
        if (File.Exists(_testDbPath))
        {
            File.Delete(_testDbPath);
        }
    }
}
