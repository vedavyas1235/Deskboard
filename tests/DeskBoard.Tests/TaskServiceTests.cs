using System;
using System.IO;
using DeskBoard.Database;
using DeskBoard.Models;
using DeskBoard.Services;
using Xunit;

namespace DeskBoard.Tests;

public class TaskServiceTests : IDisposable
{
    private readonly string _testDbPath;
    private readonly DatabaseService _databaseService;
    private readonly TaskService _taskService;

    public TaskServiceTests()
    {
        _testDbPath = Path.Combine(Path.GetTempPath(), $"test_deskboard_{Guid.NewGuid()}.db");
        _databaseService = new DatabaseService();

        // Replace the database path with test path using reflection
        var field = typeof(DatabaseService).GetField("_databasePath",
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        field?.SetValue(_databaseService, _testDbPath);

        _databaseService.Initialize();
        _taskService = new TaskService(_databaseService);
    }

    [Fact]
    public void CreateTask_ShouldReturnValidId()
    {
        // Arrange
        var task = new TaskItem
        {
            Title = "Test Task",
            Description = "Test Description",
            CreatedAt = DateTime.UtcNow,
            IsCompleted = false
        };

        // Act
        var taskId = _taskService.CreateTask(task);

        // Assert
        Assert.True(taskId > 0);
    }

    [Fact]
    public void GetTaskById_ShouldReturnCorrectTask()
    {
        // Arrange
        var task = new TaskItem
        {
            Title = "Test Task",
            Description = "Test Description",
            CreatedAt = DateTime.UtcNow,
            IsCompleted = false
        };
        var taskId = _taskService.CreateTask(task);

        // Act
        var retrievedTask = _taskService.GetTaskById(taskId);

        // Assert
        Assert.NotNull(retrievedTask);
        Assert.Equal(task.Title, retrievedTask.Title);
        Assert.Equal(task.Description, retrievedTask.Description);
    }

    [Fact]
    public void CompleteTask_ShouldMarkTaskAsCompleted()
    {
        // Arrange
        var task = new TaskItem
        {
            Title = "Test Task",
            CreatedAt = DateTime.UtcNow,
            IsCompleted = false
        };
        var taskId = _taskService.CreateTask(task);

        // Act
        _taskService.CompleteTask(taskId);
        var completedTask = _taskService.GetTaskById(taskId);

        // Assert
        Assert.NotNull(completedTask);
        Assert.True(completedTask.IsCompleted);
        Assert.NotNull(completedTask.CompletedAt);
    }

    [Fact]
    public void UncompleteTask_ShouldMarkTaskAsIncomplete()
    {
        // Arrange
        var task = new TaskItem
        {
            Title = "Test Task",
            CreatedAt = DateTime.UtcNow,
            IsCompleted = false
        };
        var taskId = _taskService.CreateTask(task);
        _taskService.CompleteTask(taskId);

        // Act
        _taskService.UncompleteTask(taskId);
        var uncompletedTask = _taskService.GetTaskById(taskId);

        // Assert
        Assert.NotNull(uncompletedTask);
        Assert.False(uncompletedTask.IsCompleted);
        Assert.Null(uncompletedTask.CompletedAt);
    }

    [Fact]
    public void DeleteTask_ShouldRemoveTask()
    {
        // Arrange
        var task = new TaskItem
        {
            Title = "Test Task",
            CreatedAt = DateTime.UtcNow,
            IsCompleted = false
        };
        var taskId = _taskService.CreateTask(task);

        // Act
        _taskService.DeleteTask(taskId);
        var deletedTask = _taskService.GetTaskById(taskId);

        // Assert
        Assert.Null(deletedTask);
    }

    [Fact]
    public void GetActiveTasks_ShouldReturnOnlyIncompleteTasks()
    {
        // Arrange
        var task1 = new TaskItem { Title = "Active Task", CreatedAt = DateTime.UtcNow, IsCompleted = false };
        var task2 = new TaskItem { Title = "Completed Task", CreatedAt = DateTime.UtcNow, IsCompleted = false };

        var taskId1 = _taskService.CreateTask(task1);
        var taskId2 = _taskService.CreateTask(task2);
        _taskService.CompleteTask(taskId2);

        // Act
        var activeTasks = _taskService.GetActiveTasks();

        // Assert
        Assert.Single(activeTasks);
        Assert.Equal("Active Task", activeTasks[0].Title);
    }

    [Fact]
    public void UpdateTask_ShouldModifyTaskProperties()
    {
        // Arrange
        var task = new TaskItem
        {
            Title = "Original Title",
            Description = "Original Description",
            CreatedAt = DateTime.UtcNow,
            IsCompleted = false
        };
        var taskId = _taskService.CreateTask(task);
        task.Id = taskId;

        // Act
        task.Title = "Updated Title";
        task.Description = "Updated Description";
        _taskService.UpdateTask(task);
        var updatedTask = _taskService.GetTaskById(taskId);

        // Assert
        Assert.NotNull(updatedTask);
        Assert.Equal("Updated Title", updatedTask.Title);
        Assert.Equal("Updated Description", updatedTask.Description);
    }

    public void Dispose()
    {
        if (File.Exists(_testDbPath))
        {
            File.Delete(_testDbPath);
        }
    }
}
