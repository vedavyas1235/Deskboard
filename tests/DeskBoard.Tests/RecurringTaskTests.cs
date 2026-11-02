using System;
using DeskBoard.Models;
using DeskBoard.Services;
using Xunit;

namespace DeskBoard.Tests;

public class RecurringTaskTests
{
    [Fact]
    public void GetNextDailyOccurrence_ShouldCalculateCorrectly()
    {
        // Arrange
        var startDate = new DateTime(2026, 10, 1);
        var fromDate = new DateTime(2026, 10, 5);
        var interval = 2;

        // Act - using reflection to test private method
        var service = new RecurringTaskService(null!, null!, null!);
        var method = typeof(RecurringTaskService).GetMethod("GetNextDailyOccurrence",
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        var result = (DateTime?)method?.Invoke(service, new object[] { startDate, interval, fromDate });

        // Assert
        Assert.NotNull(result);
        Assert.Equal(new DateTime(2026, 10, 5), result.Value);
    }

    [Fact]
    public void GetNextWeeklyOccurrence_ShouldCalculateCorrectly()
    {
        // Arrange
        var startDate = new DateTime(2026, 10, 1); // Thursday
        var fromDate = new DateTime(2026, 10, 10); // Saturday
        var interval = 1;

        // Act
        var service = new RecurringTaskService(null!, null!, null!);
        var method = typeof(RecurringTaskService).GetMethod("GetNextWeeklyOccurrence",
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        var result = (DateTime?)method?.Invoke(service, new object[] { startDate, interval, fromDate });

        // Assert
        Assert.NotNull(result);
        Assert.Equal(DayOfWeek.Thursday, result.Value.DayOfWeek);
    }

    [Fact]
    public void GetNextMonthlyOccurrence_ShouldHandleEndOfMonth()
    {
        // Arrange - Start on Jan 31
        var startDate = new DateTime(2026, 1, 31);
        var fromDate = new DateTime(2026, 2, 15);
        var interval = 1;

        // Act
        var service = new RecurringTaskService(null!, null!, null!);
        var method = typeof(RecurringTaskService).GetMethod("GetNextMonthlyOccurrence",
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        var result = (DateTime?)method?.Invoke(service, new object[] { startDate, interval, fromDate });

        // Assert
        Assert.NotNull(result);
        // February only has 28 days in 2026, so it should be Feb 28
        Assert.Equal(new DateTime(2026, 2, 28), result.Value);
    }

    [Fact]
    public void GetNextYearlyOccurrence_ShouldHandleLeapYear()
    {
        // Arrange - Start on Feb 29, 2024 (leap year)
        var startDate = new DateTime(2024, 2, 29);
        var fromDate = new DateTime(2025, 3, 1);
        var interval = 1;

        // Act
        var service = new RecurringTaskService(null!, null!, null!);
        var method = typeof(RecurringTaskService).GetMethod("GetNextYearlyOccurrence",
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        var result = (DateTime?)method?.Invoke(service, new object[] { startDate, interval, fromDate });

        // Assert
        Assert.NotNull(result);
        // 2025 is not a leap year, so it should fall back to Feb 28
        Assert.Equal(new DateTime(2025, 2, 28), result.Value);
    }

    [Theory]
    [InlineData(RecurrenceType.Daily, 1)]
    [InlineData(RecurrenceType.Weekly, 1)]
    [InlineData(RecurrenceType.Monthly, 1)]
    [InlineData(RecurrenceType.Yearly, 1)]
    public void RecurrenceType_ShouldHaveValidValues(RecurrenceType type, int expectedValue)
    {
        // Assert
        Assert.True(Enum.IsDefined(typeof(RecurrenceType), type));
        Assert.True((int)type >= 0);
    }
}
