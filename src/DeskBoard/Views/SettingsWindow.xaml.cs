using System;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using DeskBoard.Models;
using DeskBoard.Services;
using DeskBoard.ViewModels;

namespace DeskBoard.Views;

public class RecurringScheduleItem
{
    public int Id { get; set; }
    public string Title { get; set; } = string.Empty;
    public string ScheduleDescription { get; set; } = string.Empty;
}

public partial class SettingsWindow : Window
{
    private readonly MainViewModel _viewModel;

    public SettingsWindow(MainViewModel viewModel)
    {
        InitializeComponent();
        _viewModel = viewModel;
        DataContext = _viewModel;
    }

    private void Window_Loaded(object sender, RoutedEventArgs e)
    {
        LoadRecurringTasks();
    }

    private void LoadRecurringTasks()
    {
        try
        {
            var taskService = ((App)Application.Current).Services.GetService(typeof(TaskService)) as TaskService;
            if (taskService == null) return;

            var templates = taskService.GetRecurringTemplates();
            var items = templates.Select(t => new RecurringScheduleItem
            {
                Id = t.Id,
                Title = t.Title,
                ScheduleDescription = FormatScheduleDescription(t)
            }).ToList();

            RecurringTasksList.ItemsSource = items;
            NoRecurringTasksCard.Visibility = items.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
        }
        catch { }
    }

    private string FormatScheduleDescription(TaskItem t)
    {
        var freq = t.RecurrenceType.ToString();
        var start = t.RecurrenceStartDate?.ToString("MMM dd, yyyy") ?? "Today";
        if (t.RecurrenceStartDate.HasValue)
        {
            var d = t.RecurrenceStartDate.Value;
            int day = d.Day;
            string suffix = (day % 10 == 1 && day != 11) ? "st" :
                            (day % 10 == 2 && day != 12) ? "nd" :
                            (day % 10 == 3 && day != 13) ? "rd" : "th";

            return t.RecurrenceType switch
            {
                RecurrenceType.Daily => $"Repeats daily · Starts {start}",
                RecurrenceType.Weekly => $"Repeats weekly on {d:dddd} · Starts {start}",
                RecurrenceType.Monthly => $"Repeats monthly on the {day}{suffix} · Starts {start}",
                RecurrenceType.Yearly => $"Repeats yearly on {d:MMM dd} · Starts {start}",
                _ => $"{freq} · Starts {start}"
            };
        }
        return $"{freq} · Starts {start}";
    }

    private void DeleteRecurringTemplate_Click(object sender, MouseButtonEventArgs e)
    {
        if (sender is Border b && b.Tag is int templateId)
        {
            try
            {
                var taskService = ((App)Application.Current).Services.GetService(typeof(TaskService)) as TaskService;
                taskService?.DeleteRecurringTemplate(templateId);
                LoadRecurringTasks();
                _viewModel.LoadTasksCommand.Execute(null);
            }
            catch { }
        }
    }

    private void Header_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (e.ClickCount == 1) DragMove();
    }

    private void StartupCheck_Click(object sender, RoutedEventArgs e)
    {
        if (sender is CheckBox cb)
        {
            bool isEnabled = cb.IsChecked == true;
            _viewModel.Settings.StartWithWindows = isEnabled;
            _viewModel.Settings.HasExplicitlySetStartup = true;
            _viewModel.SaveSettingsCommand.Execute(null);

            var registryHelper = new DesktopIntegration.StartupRegistryHelper();
            if (isEnabled)
                registryHelper.AddToStartup();
            else
                registryHelper.RemoveFromStartup();
        }
    }

    private void Save_Click2(object sender, MouseButtonEventArgs e)
    {
        _viewModel.SaveSettingsCommand.Execute(null);
        DialogResult = true;
        Close();
    }

    private void Cancel_Click(object sender, RoutedEventArgs e)
    {
        DialogResult = false;
        Close();
    }

    private void Cancel_Click2(object sender, MouseButtonEventArgs e)
    {
        DialogResult = false;
        Close();
    }

    private void CreateBackup_Click(object sender, MouseButtonEventArgs e)
    {
        _viewModel.CreateBackupCommand.Execute(null);
    }
}
