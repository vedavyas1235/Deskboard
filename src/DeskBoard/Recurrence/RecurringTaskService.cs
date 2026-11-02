using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using DeskBoard.Database;
using DeskBoard.Models;
using DeskBoard.Services;

namespace DeskBoard.Services;

public class RecurringTaskService
{
    private readonly DatabaseService _databaseService;
    private readonly TaskService _taskService;
    private readonly NotificationService _notificationService;
    private Timer? _timer;
    private readonly TimeSpan _checkInterval = TimeSpan.FromMinutes(5);

    public RecurringTaskService(
        DatabaseService databaseService,
        TaskService taskService,
        NotificationService notificationService)
    {
        _databaseService = databaseService;
        _taskService = taskService;
        _notificationService = notificationService;
    }

    public void Start()
    {
        // Run immediately on start
        Task.Run(() => GenerateRecurringTasks());

        // Schedule periodic checks
        _timer = new Timer(
            _ => GenerateRecurringTasks(),
            null,
            _checkInterval,
            _checkInterval
        );
    }

    public void Stop()
    {
        _timer?.Dispose();
        _timer = null;
    }

    public void GenerateRecurringTasks()
    {
        try
        {
            var recurringTasks = GetRecurringTaskTemplates();
            var today = DateTime.Today;

            foreach (var template in recurringTasks)
            {
                var nextOccurrence = GetNextOccurrence(template, today);

                if (nextOccurrence == null || nextOccurrence > today)
                    continue;

                // Check if occurrence already exists
                if (OccurrenceExists(template.Id, nextOccurrence.Value))
                    continue;

                // Create new occurrence
                CreateOccurrence(template, nextOccurrence.Value);
            }
        }
        catch (Exception ex)
        {
            // Log error (in production, use proper logging)
            System.Diagnostics.Debug.WriteLine($"Error generating recurring tasks: {ex.Message}");
        }
    }

    private List<TaskItem> GetRecurringTaskTemplates()
    {
        using var connection = _databaseService.GetConnection();
        using var command = connection.CreateCommand();
        command.CommandText = @"
            SELECT Id, Title, Description, CreatedAt, CompletedAt, IsCompleted, DisplayOrder,
                   IsRecurring, RecurrenceType, RecurrenceInterval, RecurrenceStartDate,
                   RecurrenceEndDate, ParentRecurringTaskId, OccurrenceDate
            FROM Tasks
            WHERE IsRecurring = 1 AND ParentRecurringTaskId IS NULL
        ";

        var tasks = new List<TaskItem>();
        using var reader = command.ExecuteReader();
        while (reader.Read())
        {
            tasks.Add(MapTask(reader));
        }

        return tasks;
    }

    private bool OccurrenceExists(int parentTaskId, DateTime occurrenceDate)
    {
        using var connection = _databaseService.GetConnection();
        using var command = connection.CreateCommand();
        command.CommandText = @"
            SELECT COUNT(*)
            FROM Tasks
            WHERE ParentRecurringTaskId = @parentId
              AND date(OccurrenceDate) = date(@occurrenceDate)
        ";
        command.Parameters.AddWithValue("@parentId", parentTaskId);
        command.Parameters.AddWithValue("@occurrenceDate", occurrenceDate.ToString("O"));

        var count = (long)command.ExecuteScalar()!;
        return count > 0;
    }

    private void CreateOccurrence(TaskItem template, DateTime occurrenceDate)
    {
        var occurrence = new TaskItem
        {
            Title = template.Title,
            Description = template.Description,
            CreatedAt = DateTime.UtcNow,
            IsCompleted = false,
            DisplayOrder = 0,
            IsRecurring = false,
            ParentRecurringTaskId = template.Id,
            OccurrenceDate = occurrenceDate
        };

        var newId = _taskService.CreateTask(occurrence);

        // Notify user
        _notificationService.ShowNotification(
            "New Task Created",
            $"Recurring task: {occurrence.Title}"
        );
    }

    private DateTime? GetNextOccurrence(TaskItem template, DateTime fromDate)
    {
        if (!template.IsRecurring || template.RecurrenceStartDate == null)
            return null;

        var startDate = template.RecurrenceStartDate.Value.Date;
        var endDate = template.RecurrenceEndDate?.Date;

        if (fromDate < startDate)
            return startDate;

        if (endDate != null && fromDate > endDate)
            return null;

        return template.RecurrenceType switch
        {
            RecurrenceType.Daily => GetNextDailyOccurrence(startDate, template.RecurrenceInterval, fromDate),
            RecurrenceType.Weekly => GetNextWeeklyOccurrence(startDate, template.RecurrenceInterval, fromDate),
            RecurrenceType.Monthly => GetNextMonthlyOccurrence(startDate, template.RecurrenceInterval, fromDate),
            RecurrenceType.Yearly => GetNextYearlyOccurrence(startDate, template.RecurrenceInterval, fromDate),
            _ => null
        };
    }

    private DateTime? GetNextDailyOccurrence(DateTime startDate, int interval, DateTime fromDate)
    {
        var daysSinceStart = (fromDate - startDate).Days;
        var remainder = daysSinceStart % interval;

        if (remainder == 0)
            return fromDate;

        return fromDate.AddDays(interval - remainder);
    }

    private DateTime? GetNextWeeklyOccurrence(DateTime startDate, int interval, DateTime fromDate)
    {
        var weeksSinceStart = (fromDate - startDate).Days / 7;
        var remainder = weeksSinceStart % interval;

        if (remainder == 0 && fromDate.DayOfWeek == startDate.DayOfWeek)
            return fromDate;

        var nextOccurrence = startDate.AddDays((weeksSinceStart + (remainder > 0 ? interval - remainder : interval)) * 7);
        return nextOccurrence;
    }

    private DateTime? GetNextMonthlyOccurrence(DateTime startDate, int interval, DateTime fromDate)
    {
        var monthsSinceStart = ((fromDate.Year - startDate.Year) * 12) + fromDate.Month - startDate.Month;
        var remainder = monthsSinceStart % interval;

        var candidateMonth = monthsSinceStart - remainder;
        if (remainder > 0)
            candidateMonth += interval;

        var nextOccurrence = startDate.AddMonths(candidateMonth);

        // Handle edge case: if start date is 31st and target month has fewer days
        var targetDay = Math.Min(startDate.Day, DateTime.DaysInMonth(nextOccurrence.Year, nextOccurrence.Month));
        nextOccurrence = new DateTime(nextOccurrence.Year, nextOccurrence.Month, targetDay);

        if (nextOccurrence < fromDate)
            nextOccurrence = nextOccurrence.AddMonths(interval);

        return nextOccurrence;
    }

    private DateTime? GetNextYearlyOccurrence(DateTime startDate, int interval, DateTime fromDate)
    {
        var yearsSinceStart = fromDate.Year - startDate.Year;
        var remainder = yearsSinceStart % interval;

        var candidateYear = startDate.Year + (yearsSinceStart - remainder);
        if (remainder > 0)
            candidateYear += interval;

        try
        {
            var nextOccurrence = new DateTime(candidateYear, startDate.Month, startDate.Day);

            if (nextOccurrence < fromDate)
                nextOccurrence = nextOccurrence.AddYears(interval);

            return nextOccurrence;
        }
        catch (ArgumentOutOfRangeException)
        {
            // Handle Feb 29 on non-leap years
            var nextOccurrence = new DateTime(candidateYear, startDate.Month, 28);
            if (nextOccurrence < fromDate)
                nextOccurrence = nextOccurrence.AddYears(interval);
            return nextOccurrence;
        }
    }

    private TaskItem MapTask(Microsoft.Data.Sqlite.SqliteDataReader reader)
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
