using System;
using System.Collections.Generic;
using DeskBoard.Database;
using DeskBoard.Models;
using Microsoft.Data.Sqlite;

namespace DeskBoard.Services;

public class TaskService
{
    private readonly DatabaseService _databaseService;

    public TaskService(DatabaseService databaseService)
    {
        _databaseService = databaseService;
    }

    public List<TaskItem> GetAllTasks()
    {
        var tasks = new List<TaskItem>();

        using var connection = _databaseService.GetConnection();
        using var command = connection.CreateCommand();
        // Templates (IsRecurring = 1 AND ParentRecurringTaskId IS NULL) are schedules, not daily board tasks.
        // The board displays regular tasks and scheduled occurrences that have been generated.
        command.CommandText = @"
            SELECT Id, Title, Description, CreatedAt, CompletedAt, IsCompleted, DisplayOrder,
                   IsRecurring, RecurrenceType, RecurrenceInterval, RecurrenceStartDate,
                   RecurrenceEndDate, ParentRecurringTaskId, OccurrenceDate
            FROM Tasks
            WHERE (IsRecurring = 0 OR ParentRecurringTaskId IS NOT NULL)
            ORDER BY IsCompleted ASC, CreatedAt DESC
        ";

        using var reader = command.ExecuteReader();
        while (reader.Read())
        {
            tasks.Add(MapTask(reader));
        }

        return tasks;
    }

    public List<TaskItem> GetActiveTasks()
    {
        var tasks = new List<TaskItem>();

        using var connection = _databaseService.GetConnection();
        using var command = connection.CreateCommand();
        command.CommandText = @"
            SELECT Id, Title, Description, CreatedAt, CompletedAt, IsCompleted, DisplayOrder,
                   IsRecurring, RecurrenceType, RecurrenceInterval, RecurrenceStartDate,
                   RecurrenceEndDate, ParentRecurringTaskId, OccurrenceDate
            FROM Tasks
            WHERE IsCompleted = 0 AND (IsRecurring = 0 OR ParentRecurringTaskId IS NOT NULL)
            ORDER BY DisplayOrder, CreatedAt DESC
        ";

        using var reader = command.ExecuteReader();
        while (reader.Read())
        {
            tasks.Add(MapTask(reader));
        }

        return tasks;
    }

    public List<TaskItem> GetRecurringTemplates()
    {
        var tasks = new List<TaskItem>();

        using var connection = _databaseService.GetConnection();
        using var command = connection.CreateCommand();
        command.CommandText = @"
            SELECT Id, Title, Description, CreatedAt, CompletedAt, IsCompleted, DisplayOrder,
                   IsRecurring, RecurrenceType, RecurrenceInterval, RecurrenceStartDate,
                   RecurrenceEndDate, ParentRecurringTaskId, OccurrenceDate
            FROM Tasks
            WHERE IsRecurring = 1 AND ParentRecurringTaskId IS NULL
            ORDER BY CreatedAt DESC
        ";

        using var reader = command.ExecuteReader();
        while (reader.Read())
        {
            tasks.Add(MapTask(reader));
        }

        return tasks;
    }

    public void DeleteRecurringTemplate(int templateId)
    {
        using var connection = _databaseService.GetConnection();
        using var command = connection.CreateCommand();
        command.CommandText = "DELETE FROM Tasks WHERE Id = @id OR ParentRecurringTaskId = @id";
        command.Parameters.AddWithValue("@id", templateId);
        command.ExecuteNonQuery();
    }

    public TaskItem? GetTaskById(int id)
    {
        using var connection = _databaseService.GetConnection();
        using var command = connection.CreateCommand();
        command.CommandText = @"
            SELECT Id, Title, Description, CreatedAt, CompletedAt, IsCompleted, DisplayOrder,
                   IsRecurring, RecurrenceType, RecurrenceInterval, RecurrenceStartDate,
                   RecurrenceEndDate, ParentRecurringTaskId, OccurrenceDate
            FROM Tasks
            WHERE Id = @id
        ";
        command.Parameters.AddWithValue("@id", id);

        using var reader = command.ExecuteReader();
        if (reader.Read())
        {
            return MapTask(reader);
        }

        return null;
    }

    public int CreateTask(TaskItem task)
    {
        if (task.DisplayOrder == 0)
        {
            task.DisplayOrder = GetNextDisplayOrder();
        }

        using var connection = _databaseService.GetConnection();
        using var command = connection.CreateCommand();
        command.CommandText = @"
            INSERT INTO Tasks (Title, Description, CreatedAt, CompletedAt, IsCompleted, DisplayOrder,
                              IsRecurring, RecurrenceType, RecurrenceInterval, RecurrenceStartDate,
                              RecurrenceEndDate, ParentRecurringTaskId, OccurrenceDate)
            VALUES (@title, @description, @createdAt, @completedAt, @isCompleted, @displayOrder,
                    @isRecurring, @recurrenceType, @recurrenceInterval, @recurrenceStartDate,
                    @recurrenceEndDate, @parentRecurringTaskId, @occurrenceDate);
            SELECT last_insert_rowid();
        ";

        AddTaskParameters(command, task);
        var id = (long)command.ExecuteScalar()!;
        return (int)id;
    }

    public void UpdateTask(TaskItem task)
    {
        using var connection = _databaseService.GetConnection();
        using var command = connection.CreateCommand();
        command.CommandText = @"
            UPDATE Tasks
            SET Title = @title,
                Description = @description,
                CompletedAt = @completedAt,
                IsCompleted = @isCompleted,
                DisplayOrder = @displayOrder,
                IsRecurring = @isRecurring,
                RecurrenceType = @recurrenceType,
                RecurrenceInterval = @recurrenceInterval,
                RecurrenceStartDate = @recurrenceStartDate,
                RecurrenceEndDate = @recurrenceEndDate,
                ParentRecurringTaskId = @parentRecurringTaskId,
                OccurrenceDate = @occurrenceDate
            WHERE Id = @id
        ";

        AddTaskParameters(command, task);
        command.Parameters.AddWithValue("@id", task.Id);
        command.ExecuteNonQuery();
    }

    public void DeleteTask(int id)
    {
        using var connection = _databaseService.GetConnection();
        using var command = connection.CreateCommand();
        command.CommandText = "DELETE FROM Tasks WHERE Id = @id";
        command.Parameters.AddWithValue("@id", id);
        command.ExecuteNonQuery();
    }

    public void CompleteTask(int id)
    {
        using var connection = _databaseService.GetConnection();
        using var command = connection.CreateCommand();
        command.CommandText = @"
            UPDATE Tasks
            SET IsCompleted = 1, CompletedAt = @completedAt
            WHERE Id = @id
        ";
        command.Parameters.AddWithValue("@completedAt", DateTime.UtcNow.ToString("O"));
        command.Parameters.AddWithValue("@id", id);
        command.ExecuteNonQuery();
    }

    public void UncompleteTask(int id)
    {
        using var connection = _databaseService.GetConnection();
        using var command = connection.CreateCommand();
        command.CommandText = @"
            UPDATE Tasks
            SET IsCompleted = 0, CompletedAt = NULL
            WHERE Id = @id
        ";
        command.Parameters.AddWithValue("@id", id);
        command.ExecuteNonQuery();
    }

    public void ReorderTask(int taskId, int newDisplayOrder)
    {
        using var connection = _databaseService.GetConnection();
        using var command = connection.CreateCommand();
        command.CommandText = @"
            UPDATE Tasks
            SET DisplayOrder = @displayOrder
            WHERE Id = @id
        ";
        command.Parameters.AddWithValue("@displayOrder", newDisplayOrder);
        command.Parameters.AddWithValue("@id", taskId);
        command.ExecuteNonQuery();
    }

    private int GetNextDisplayOrder()
    {
        return _databaseService.ExecuteScalar<int>(
            "SELECT COALESCE(MAX(DisplayOrder), 0) + 1 FROM Tasks"
        );
    }

    private void AddTaskParameters(SqliteCommand command, TaskItem task)
    {
        command.Parameters.AddWithValue("@title", task.Title);
        command.Parameters.AddWithValue("@description", (object?)task.Description ?? DBNull.Value);
        command.Parameters.AddWithValue("@createdAt", task.CreatedAt.ToString("O"));
        command.Parameters.AddWithValue("@completedAt", task.CompletedAt?.ToString("O") ?? (object)DBNull.Value);
        command.Parameters.AddWithValue("@isCompleted", task.IsCompleted ? 1 : 0);
        command.Parameters.AddWithValue("@displayOrder", task.DisplayOrder);
        command.Parameters.AddWithValue("@isRecurring", task.IsRecurring ? 1 : 0);
        command.Parameters.AddWithValue("@recurrenceType", (int)task.RecurrenceType);
        command.Parameters.AddWithValue("@recurrenceInterval", task.RecurrenceInterval);
        command.Parameters.AddWithValue("@recurrenceStartDate", task.RecurrenceStartDate?.ToString("O") ?? (object)DBNull.Value);
        command.Parameters.AddWithValue("@recurrenceEndDate", task.RecurrenceEndDate?.ToString("O") ?? (object)DBNull.Value);
        command.Parameters.AddWithValue("@parentRecurringTaskId", (object?)task.ParentRecurringTaskId ?? DBNull.Value);
        command.Parameters.AddWithValue("@occurrenceDate", task.OccurrenceDate?.ToString("O") ?? (object)DBNull.Value);
    }

    private TaskItem MapTask(SqliteDataReader reader)
    {
        return new TaskItem
        {
            Id = reader.GetInt32(0),
            Title = reader.GetString(1),
            Description = reader.IsDBNull(2) ? null : reader.GetString(2),
            CreatedAt = DateTime.Parse(reader.GetString(3)),
            CompletedAt = reader.IsDBNull(4) ? null : DateTime.Parse(reader.GetString(4)),
            IsCompleted = reader.GetInt32(5) == 1,
            DisplayOrder = reader.GetInt32(6),
            IsRecurring = reader.GetInt32(7) == 1,
            RecurrenceType = (RecurrenceType)reader.GetInt32(8),
            RecurrenceInterval = reader.GetInt32(9),
            RecurrenceStartDate = reader.IsDBNull(10) ? null : DateTime.Parse(reader.GetString(10)),
            RecurrenceEndDate = reader.IsDBNull(11) ? null : DateTime.Parse(reader.GetString(11)),
            ParentRecurringTaskId = reader.IsDBNull(12) ? null : reader.GetInt32(12),
            OccurrenceDate = reader.IsDBNull(13) ? null : DateTime.Parse(reader.GetString(13))
        };
    }
}
