using System;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using DeskBoard.Models;
using DeskBoard.Services;
using DeskBoard.ViewModels;

namespace DeskBoard.Views;

public partial class AddTaskDialog : Window
{
    private readonly MainViewModel _viewModel;
    private bool _isScheduled = false;

    // Clock hands - live rotation driven by current time
    private System.Windows.Threading.DispatcherTimer? _clockTimer;

    public AddTaskDialog(MainViewModel viewModel)
    {
        InitializeComponent();
        _viewModel = viewModel;

        // Initialise date picker
        StartDatePicker.SelectedDate = DateTime.Today;
        StartDatePicker.SelectedDateChanged += (s, e) => UpdateScheduleSummary();
        FrequencyCombo.SelectionChanged         += (s, e) => UpdateScheduleSummary();

        // Start clock animation
        StartClock();

        // Default: Regular tab active
        ActivateRegularTab();
    }

    private void Window_Loaded(object sender, RoutedEventArgs e)
    {
        TitleInput.Focus();
    }

    // ── Tab switching ─────────────────────────────────────────────
    private void TabRegular_Click(object sender, MouseButtonEventArgs e)
    {
        _isScheduled = false;
        ActivateRegularTab();
        AnimateContentIn();
    }

    private void TabScheduled_Click(object sender, MouseButtonEventArgs e)
    {
        _isScheduled = true;
        ActivateScheduledTab();
        AnimateContentIn();
    }

    private void ActivateRegularTab()
    {
        RecurringPanel.Visibility = Visibility.Collapsed;

        // Tab Regular: glowing accent background
        TabRegular.Background = GetAccentBrush();
        TabRegular.BorderBrush = new SolidColorBrush(Color.FromArgb(100, 108, 99, 255));
        TabRegularText.Foreground = Brushes.White;

        TabScheduled.Background  = new SolidColorBrush(Color.FromArgb(0x18, 255, 255, 255));
        TabScheduled.BorderBrush = new SolidColorBrush(Color.FromArgb(0x20, 255, 255, 255));
        TabScheduledText.Foreground = new SolidColorBrush(Color.FromArgb(0xAA, 255, 255, 255));

        AddButton.Content = "Add Task";
    }

    private void ActivateScheduledTab()
    {
        RecurringPanel.Visibility = Visibility.Visible;
        UpdateScheduleSummary();

        TabScheduled.Background  = GetAccentBrush();
        TabScheduled.BorderBrush = new SolidColorBrush(Color.FromArgb(100, 167, 139, 250));
        TabScheduledText.Foreground = Brushes.White;

        TabRegular.Background  = new SolidColorBrush(Color.FromArgb(0x18, 255, 255, 255));
        TabRegular.BorderBrush = new SolidColorBrush(Color.FromArgb(0x20, 255, 255, 255));
        TabRegularText.Foreground = new SolidColorBrush(Color.FromArgb(0xAA, 255, 255, 255));

        AddButton.Content = "Schedule Task";
    }

    private LinearGradientBrush GetAccentBrush() => new LinearGradientBrush(
        Color.FromRgb(108, 99, 255),
        Color.FromRgb(167, 139, 250),
        new Point(0, 0), new Point(1, 1));

    private void AnimateContentIn()
    {
        ContentPanel.Opacity = 0;
        var anim = new DoubleAnimation(0, 1, TimeSpan.FromMilliseconds(220))
        {
            EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut }
        };
        ContentPanel.BeginAnimation(UIElement.OpacityProperty, anim);
    }

    // ── Clock animation ───────────────────────────────────────────
    private void StartClock()
    {
        UpdateClockHands();
        _clockTimer = new System.Windows.Threading.DispatcherTimer
        {
            Interval = TimeSpan.FromSeconds(1)
        };
        _clockTimer.Tick += (_, _) => UpdateClockHands();
        _clockTimer.Start();
    }

    private void UpdateClockHands()
    {
        var now = DateTime.Now;
        double minutes = now.Minute + now.Second / 60.0;
        double hours   = (now.Hour % 12) + minutes / 60.0;

        // Minute hand: 6° per minute
        var minuteAnim = new DoubleAnimation
        {
            To = minutes * 6,
            Duration = TimeSpan.FromMilliseconds(600),
            EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut }
        };
        MinuteHandRotate.BeginAnimation(System.Windows.Media.RotateTransform.AngleProperty, minuteAnim);

        // Hour hand: 30° per hour
        var hourAnim = new DoubleAnimation
        {
            To = hours * 30,
            Duration = TimeSpan.FromMilliseconds(600),
            EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut }
        };
        HourHandRotate.BeginAnimation(System.Windows.Media.RotateTransform.AngleProperty, hourAnim);
    }

    // ── Schedule summary text ─────────────────────────────────────
    private void UpdateScheduleSummary()
    {
        if (FrequencyCombo.SelectedItem is not ComboBoxItem item) return;
        var freq = item.Content?.ToString() ?? "Daily";
        var date = StartDatePicker.SelectedDate ?? DateTime.Today;
        var dateStr = date == DateTime.Today ? "today" : date.ToString("MMM d, yyyy");
        ScheduleSummary.Text = $"Every {freq.ToLower()} starting {dateStr}";
    }

    // ── Add task ──────────────────────────────────────────────────
    private void AddTask_Click(object sender, RoutedEventArgs e)
    {
        var title = TitleInput.Text?.Trim();
        if (string.IsNullOrWhiteSpace(title))
        {
            ShakeInput();
            return;
        }

        if (_isScheduled)
            CreateRecurringTask(title);
        else
            CreateRegularTask(title);

        _clockTimer?.Stop();
        DialogResult = true;
        Close();
    }

    private void CreateRegularTask(string title)
    {
        var desc = DescInput.Text?.Trim();
        _viewModel.NewTaskTitle       = title;
        _viewModel.NewTaskDescription = string.IsNullOrEmpty(desc) ? null : desc;
        _viewModel.AddTaskCommand.Execute(null);
    }

    private void CreateRecurringTask(string title)
    {
        var frequencyItem = FrequencyCombo.SelectedItem as ComboBoxItem;
        var frequency = frequencyItem?.Content?.ToString() ?? "Daily";
        var startDate = StartDatePicker.SelectedDate ?? DateTime.Today;
        var desc = DescInput.Text?.Trim();

        var recurrenceType = frequency switch
        {
            "Daily"   => RecurrenceType.Daily,
            "Weekly"  => RecurrenceType.Weekly,
            "Monthly" => RecurrenceType.Monthly,
            "Yearly"  => RecurrenceType.Yearly,
            _         => RecurrenceType.Daily
        };

        var task = new TaskItem
        {
            Title              = title,
            Description        = string.IsNullOrEmpty(desc) ? null : desc,
            CreatedAt          = DateTime.UtcNow,
            IsCompleted        = false,
            IsRecurring        = true,
            RecurrenceType     = recurrenceType,
            RecurrenceInterval = 1,
            RecurrenceStartDate = startDate,
            RecurrenceEndDate   = null
        };

        try
        {
            var taskService = ((App)Application.Current)
                .Services.GetService(typeof(TaskService)) as TaskService;
            taskService?.CreateTask(task);
            _viewModel.LoadTasksCommand.Execute(null);
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Error creating scheduled task: {ex.Message}", "Error",
                MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    // ── Shake animation for empty title ──────────────────────────
    private void ShakeInput()
    {
        var anim = new DoubleAnimationUsingKeyFrames();
        anim.KeyFrames.Add(new EasingDoubleKeyFrame(0,   KeyTime.FromTimeSpan(TimeSpan.Zero)));
        anim.KeyFrames.Add(new EasingDoubleKeyFrame(-8,  KeyTime.FromTimeSpan(TimeSpan.FromMilliseconds(60))));
        anim.KeyFrames.Add(new EasingDoubleKeyFrame( 8,  KeyTime.FromTimeSpan(TimeSpan.FromMilliseconds(120))));
        anim.KeyFrames.Add(new EasingDoubleKeyFrame(-6,  KeyTime.FromTimeSpan(TimeSpan.FromMilliseconds(180))));
        anim.KeyFrames.Add(new EasingDoubleKeyFrame( 4,  KeyTime.FromTimeSpan(TimeSpan.FromMilliseconds(240))));
        anim.KeyFrames.Add(new EasingDoubleKeyFrame(0,   KeyTime.FromTimeSpan(TimeSpan.FromMilliseconds(300))));

        var transform = new TranslateTransform();
        TitleInput.RenderTransform = transform;
        transform.BeginAnimation(TranslateTransform.XProperty, anim);
        TitleInput.Focus();
    }

    private void Close_Click(object sender, RoutedEventArgs e)
    {
        _clockTimer?.Stop();
        DialogResult = false;
        Close();
    }

    private void TitleInput_KeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter)
        {
            AddTask_Click(sender, e);
            e.Handled = true;
        }
    }
}
