using System;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using DeskBoard.Models;
using DeskBoard.Services;

namespace DeskBoard.ViewModels;

public partial class TaskItemViewModel : ObservableObject
{
    private readonly TaskService _taskService;
    private readonly NotificationService _notificationService;

    [ObservableProperty]
    private TaskItem _task;

    [ObservableProperty]
    private bool _isEditing;

    [ObservableProperty]
    private string _editTitle = string.Empty;

    [ObservableProperty]
    private string? _editDescription;

    public TaskItemViewModel(TaskItem task, TaskService taskService, NotificationService notificationService)
    {
        _task = task;
        _taskService = taskService;
        _notificationService = notificationService;
    }

    [RelayCommand]
    private void ToggleComplete()
    {
        if (Task.IsCompleted)
        {
            _taskService.UncompleteTask(Task.Id);
            Task.IsCompleted = false;
            Task.CompletedAt = null;
        }
        else
        {
            _taskService.CompleteTask(Task.Id);
            Task.IsCompleted = true;
            Task.CompletedAt = DateTime.UtcNow;
            _notificationService.ShowTaskCompleted(Task.Title);
        }

        OnPropertyChanged(nameof(Task));
    }

    [RelayCommand]
    private void StartEdit()
    {
        EditTitle = Task.Title;
        EditDescription = Task.Description;
        IsEditing = true;
    }

    [RelayCommand]
    private void SaveEdit()
    {
        if (string.IsNullOrWhiteSpace(EditTitle))
            return;

        Task.Title = EditTitle.Trim();
        Task.Description = string.IsNullOrWhiteSpace(EditDescription) ? null : EditDescription.Trim();

        _taskService.UpdateTask(Task);
        IsEditing = false;
        OnPropertyChanged(nameof(Task));
    }

    [RelayCommand]
    private void CancelEdit()
    {
        IsEditing = false;
        EditTitle = string.Empty;
        EditDescription = null;
    }

    [RelayCommand]
    private void Delete()
    {
        _taskService.DeleteTask(Task.Id);
    }
}
