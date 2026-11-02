using System;

namespace DeskBoard.Models;

public class TaskItem
{
    public int Id { get; set; }
    public string Title { get; set; } = string.Empty;
    public string? Description { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? CompletedAt { get; set; }
    public bool IsCompleted { get; set; }
    public int DisplayOrder { get; set; }

    // Recurring task properties
    public bool IsRecurring { get; set; }
    public RecurrenceType RecurrenceType { get; set; }
    public int RecurrenceInterval { get; set; } = 1;
    public DateTime? RecurrenceStartDate { get; set; }
    public DateTime? RecurrenceEndDate { get; set; }
    public int? ParentRecurringTaskId { get; set; }
    public DateTime? OccurrenceDate { get; set; }
}

public enum RecurrenceType
{
    None = 0,
    Daily = 1,
    Weekly = 2,
    Monthly = 3,
    Yearly = 4
}
