using System;
using System.Collections.ObjectModel;
using System.Linq;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using DeskBoard.Models;
using DeskBoard.Services;

namespace DeskBoard.ViewModels;

public partial class MainViewModel : ObservableObject
{
    private readonly TaskService _taskService;
    private readonly NotificationService _notificationService;
    private readonly SettingsService _settingsService;
    private readonly BackupService _backupService;

    [ObservableProperty]
    private ObservableCollection<TaskItemViewModel> _tasks = new();

    [ObservableProperty]
    private string _newTaskTitle = string.Empty;

    [ObservableProperty]
    private string? _newTaskDescription;

    [ObservableProperty]
    private bool _showCompleted = true;

    [ObservableProperty]
    private AppSettings _settings;

    public MainViewModel(
        TaskService taskService,
        NotificationService notificationService,
        SettingsService settingsService,
        BackupService backupService)
    {
        _taskService = taskService;
        _notificationService = notificationService;
        _settingsService = settingsService;
        _backupService = backupService;
        _settings = _settingsService.GetSettings();

        LoadTasks();
    }

    [RelayCommand]
    private void AddTask()
    {
        if (string.IsNullOrWhiteSpace(NewTaskTitle))
            return;

        try
        {
            var task = new TaskItem
            {
                Title = NewTaskTitle.Trim(),
                Description = string.IsNullOrWhiteSpace(NewTaskDescription) ? null : NewTaskDescription.Trim(),
                CreatedAt = DateTime.UtcNow,
                IsCompleted = false
            };

            var taskId = _taskService.CreateTask(task);
            task.Id = taskId;

            LoadTasks();
            _notificationService.ShowTaskCreated(task.Title);

            // Clear input
            NewTaskTitle = string.Empty;
            NewTaskDescription = null;
        }
        catch (Exception ex)
        {
            System.Windows.MessageBox.Show($"Error adding task: {ex.Message}", "Error");
        }
    }

    [RelayCommand]
    private void LoadTasks()
    {
        try
        {
            var tasks = ShowCompleted
                ? _taskService.GetAllTasks()
                : _taskService.GetActiveTasks();

            Tasks.Clear();
            foreach (var task in tasks)
            {
                var viewModel = new TaskItemViewModel(task, _taskService, _notificationService);
                Tasks.Add(viewModel);
            }
        }
        catch (Exception ex)
        {
            System.Windows.MessageBox.Show($"Error loading tasks: {ex.Message}", "Error");
        }
    }

    [RelayCommand]
    private void ToggleShowCompleted()
    {
        ShowCompleted = !ShowCompleted;
        LoadTasks();
    }

    [RelayCommand]
    private void CreateBackup()
    {
        try
        {
            _backupService.CreateBackup();
            // Silent — no popup; backup created in background
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Backup error: {ex.Message}");
        }
    }

    [RelayCommand]
    private void SaveSettings()
    {
        _settingsService.SaveSettings(Settings);
        // Silent save — no popup
    }

    [RelayCommand]
    private void ToggleStartWithWindows()
    {
        _settingsService.UpdateStartWithWindows(!Settings.StartWithWindows);
        Settings = _settingsService.GetSettings();
        OnPropertyChanged(nameof(Settings));
    }

    partial void OnShowCompletedChanged(bool value)
    {
        LoadTasks();
    }
}
