using System;
using System.Linq;
using System.Runtime;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Threading;
using DeskBoard.DesktopIntegration;
using DeskBoard.Models;
using DeskBoard.ViewModels;

namespace DeskBoard.Views;

public partial class MainWindow : Window
{
    private readonly MainViewModel _viewModel;
    private readonly StartupRegistryHelper _startupHelper = new();

    // ── Watermark & Hover Opacity ───────────────────────────────────
    private const double GHOST_OPACITY  = 0.18;
    private const double ACTIVE_OPACITY = 1.0;

    // ── Proportional Resizing Base Width ───────────────────────────
    private const double BASE_WIDTH = 300.0;

    // ── State flags ────────────────────────────────────────────────
    private bool _isAddPanelOpen    = false;
    private bool _isRecurring       = false;
    private bool _isCalendarOpen    = false;
    private string _repeatFrequency = "Weekly";
    private DateTime _selectedDate  = DateTime.Today;
    private DateTime _calendarMonth = DateTime.Today;
    private DispatcherTimer? _initialFadeTimer;

    public MainWindow(MainViewModel viewModel)
    {
        InitializeComponent();
        _viewModel = viewModel;
        DataContext = _viewModel;

        try
        {
            Icon = System.Windows.Media.Imaging.BitmapFrame.Create(
                new Uri("pack://application:,,,/Assets/app.ico", UriKind.RelativeOrAbsolute));
        }
        catch { }

        // Start fully visible on startup
        Opacity = ACTIVE_OPACITY;

        // Auto update count
        _viewModel.Tasks.CollectionChanged += (_, __) => UpdateCount();

        // After 4 seconds, fade to subtle watermark if not hovered
        _initialFadeTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(4) };
        _initialFadeTimer.Tick += (s, e) =>
        {
            _initialFadeTimer.Stop();
            var pos = Mouse.GetPosition(this);
            bool mouseOver = pos.X >= 0 && pos.Y >= 0 && pos.X <= ActualWidth && pos.Y <= ActualHeight;
            if (!mouseOver && !_isAddPanelOpen)
            {
                AnimateOpacity(GHOST_OPACITY);
            }
        };
        _initialFadeTimer.Start();
    }

    private void Window_Loaded(object sender, RoutedEventArgs e)
    {
        // By default Topmost is FALSE so it sticks to desktop wallpaper!
        // Only floats over other windows if user explicitly enables "Always on Top"
        Topmost = _viewModel.Settings.AlwaysOnTop;

        // Restore saved size if valid
        if (_viewModel.Settings.WindowWidth >= 210 && _viewModel.Settings.WindowWidth <= 390)
        {
            Width = _viewModel.Settings.WindowWidth;
            double scale = Width / BASE_WIDTH;
            BoardScale.ScaleX = scale;
            BoardScale.ScaleY = scale;
        }

        // Restore saved position or default to top-right
        if (_viewModel.Settings.WindowLeft >= 0 && _viewModel.Settings.WindowTop >= 0)
        {
            Left = _viewModel.Settings.WindowLeft;
            Top = _viewModel.Settings.WindowTop;
        }
        else
        {
            PositionTopRight();
        }

        UpdateCount();
        DateLabel.Text = _selectedDate.ToString("MMM dd, yyyy");
        UpdateRepeatCheckmarks();
        UpdateRecurrenceSummary();

        // Ensure Windows Startup is registered so DeskBoard launches on every reboot
        if (_viewModel.Settings.StartWithWindows)
            _startupHelper.AddToStartup();
    }

    private void PositionTopRight()
    {
        var area = SystemParameters.WorkArea;
        double w = (ActualWidth > 50) ? ActualWidth : (Width > 50 ? Width : 300);
        Left = area.Right - w - 24;
        Top  = area.Top + 24;
    }

    // ── Dynamic Proportional Resizing (70% to 130%) ────────────────
    private bool _isResizing = false;
    private Point _resizeStartMouse;
    private double _resizeStartWidth;

    private void ResizeGrip_MouseDown(object sender, MouseButtonEventArgs e)
    {
        if (e.LeftButton == MouseButtonState.Pressed)
        {
            _isResizing = true;
            _resizeStartMouse = PointToScreen(e.GetPosition(this));
            _resizeStartWidth = this.ActualWidth > 50 ? this.ActualWidth : this.Width;
            ((UIElement)sender).CaptureMouse();
            e.Handled = true;
        }
    }

    private void ResizeGrip_MouseMove(object sender, MouseEventArgs e)
    {
        if (_isResizing)
        {
            Point currentMouse = PointToScreen(e.GetPosition(this));
            double deltaX = currentMouse.X - _resizeStartMouse.X;
            double newWidth = _resizeStartWidth + deltaX;

            // Clamped between 210 (70% of 300) and 390 (130% of 300)
            newWidth = Math.Clamp(newWidth, 210, 390);

            this.Width = newWidth;

            double scale = newWidth / BASE_WIDTH;
            BoardScale.ScaleX = scale;
            BoardScale.ScaleY = scale;
        }
    }

    private void ResizeGrip_MouseUp(object sender, MouseButtonEventArgs e)
    {
        if (_isResizing)
        {
            _isResizing = false;
            ((UIElement)sender).ReleaseMouseCapture();
        }
    }

    private void Window_SizeChanged(object sender, SizeChangedEventArgs e)
    {
        if (e.WidthChanged && e.NewSize.Width > 50)
        {
            double scale = e.NewSize.Width / BASE_WIDTH;
            scale = Math.Clamp(scale, 0.70, 1.30);

            BoardScale.ScaleX = scale;
            BoardScale.ScaleY = scale;
        }
    }

    public void WakeUpWindow()
    {
        AnimateOpacity(ACTIVE_OPACITY);
        if (WindowState == WindowState.Minimized)
            WindowState = WindowState.Normal;
        Activate();
        Focus();
    }

    // ── P/Invoke: Ask Windows to trim the process working set when idle ──
    [DllImport("kernel32.dll")]
    private static extern bool SetProcessWorkingSetSize(IntPtr hProcess, nint dwMinimumWorkingSetSize, nint dwMaximumWorkingSetSize);

    private static void TrimWorkingSet()
    {
        // Force .NET GC to collect unreachable objects
        GCSettings.LargeObjectHeapCompactionMode = GCLargeObjectHeapCompactionMode.CompactOnce;
        GC.Collect(2, GCCollectionMode.Forced, true, true);
        GC.WaitForPendingFinalizers();
        GC.Collect(2, GCCollectionMode.Forced, true, true);

        // Tell Windows to release idle RAM pages back to the OS page pool
        // -1/-1 means: trim to minimum (Windows re-expands on next access instantly)
        SetProcessWorkingSetSize(System.Diagnostics.Process.GetCurrentProcess().Handle, -1, -1);
    }

    // ── Hover Fade ─────────────────────────────────────────────────
    private void Window_MouseEnter(object sender, MouseEventArgs e)
    {
        AnimateOpacity(ACTIVE_OPACITY);
    }

    private void Window_MouseLeave(object sender, MouseEventArgs e)
    {
        if (!_isAddPanelOpen)
        {
            AnimateOpacity(GHOST_OPACITY);

            // Trim idle RAM — pages restore instantly when mouse re-enters
            // Run on background thread so it never blocks the UI
            System.Threading.Tasks.Task.Run(TrimWorkingSet);
        }
    }

    private void AnimateOpacity(double target)
    {
        var anim = new DoubleAnimation
        {
            To = target,
            Duration = TimeSpan.FromMilliseconds(240),
            EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut }
        };
        BeginAnimation(OpacityProperty, anim);
    }

    // ── Drag Window ────────────────────────────────────────────────
    private void Header_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (e.ClickCount == 1)
        {
            try
            {
                DragMove();
            }
            catch { }
        }
    }

    // ── Toggle Add Task Panel (+ / ✕) ──────────────────────────────
    private void AddToggleBtn_Click(object sender, MouseButtonEventArgs e)
    {
        if (_isAddPanelOpen)
        {
            CloseAddPanel();
        }
        else
        {
            OpenAddPanel();
        }
    }

    private void OpenAddPanel()
    {
        _isAddPanelOpen = true;
        AnimateOpacity(ACTIVE_OPACITY);

        // Rotate '+' to '✕' (45 degrees)
        AnimateIcon(45);

        AddPanel.Visibility = Visibility.Visible;
        TitleInput.Focus();
    }

    private void CloseAddPanel()
    {
        _isAddPanelOpen = false;

        // Rotate '✕' back to '+' (0 degrees)
        AnimateIcon(0);

        AddPanel.Visibility = Visibility.Collapsed;
        CalendarPopupCard.Visibility = Visibility.Collapsed;
        RepeatOptionsCard.Visibility = Visibility.Collapsed;
        _isCalendarOpen = false;

        // Reset inputs
        TitleInput.Clear();
        ResetRecurringForm();

        // If mouse is outside, return to watermark
        var mousePos = Mouse.GetPosition(this);
        if (mousePos.X < 0 || mousePos.Y < 0 || mousePos.X > ActualWidth || mousePos.Y > ActualHeight)
        {
            AnimateOpacity(GHOST_OPACITY);
        }
    }

    private void AnimateIcon(double angle)
    {
        var anim = new DoubleAnimation
        {
            To = angle,
            Duration = TimeSpan.FromMilliseconds(220),
            EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut }
        };
        PlusIconRotate.BeginAnimation(RotateTransform.AngleProperty, anim);
    }

    // ── Input Placeholder ──────────────────────────────────────────
    private void TitleInput_TextChanged(object sender, TextChangedEventArgs e)
    {
        PlaceholderText.Visibility = string.IsNullOrEmpty(TitleInput.Text)
            ? Visibility.Visible
            : Visibility.Collapsed;
    }

    private void TitleInput_KeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter)
        {
            SubmitTask();
            e.Handled = true;
        }
        else if (e.Key == Key.Escape)
        {
            CloseAddPanel();
            e.Handled = true;
        }
    }

    // ── Simple / Recurring Tab Switching ───────────────────────────
    private void SimpleTab_Click(object sender, MouseButtonEventArgs e)
    {
        _isRecurring = false;
        SimpleTab.Background = new SolidColorBrush(Color.FromRgb(0x26, 0x30, 0x42));
        SimpleTab.BorderBrush = new SolidColorBrush(Color.FromArgb(0x38, 0xFF, 0xFF, 0xFF));

        RecurringTab.Background = Brushes.Transparent;
        RecurringTab.BorderBrush = new SolidColorBrush(Color.FromArgb(0x18, 0xFF, 0xFF, 0xFF));
        RecurringTabText.Foreground = new SolidColorBrush(Color.FromArgb(0x75, 0xFF, 0xFF, 0xFF));
        RecurringTabText.FontWeight = FontWeights.Normal;

        RecurringSection.Visibility = Visibility.Collapsed;
        RepeatOptionsCard.Visibility = Visibility.Collapsed;
        CalendarPopupCard.Visibility = Visibility.Collapsed;
        _isCalendarOpen = false;
    }

    private void RecurringTab_Click(object sender, MouseButtonEventArgs e)
    {
        _isRecurring = true;
        RecurringTab.Background = new SolidColorBrush(Color.FromRgb(0x26, 0x30, 0x42));
        RecurringTab.BorderBrush = new SolidColorBrush(Color.FromArgb(0x38, 0xFF, 0xFF, 0xFF));
        RecurringTabText.Foreground = Brushes.White;
        RecurringTabText.FontWeight = FontWeights.Medium;

        SimpleTab.Background = Brushes.Transparent;
        SimpleTab.BorderBrush = new SolidColorBrush(Color.FromArgb(0x18, 0xFF, 0xFF, 0xFF));

        RecurringSection.Visibility = Visibility.Visible;
        UpdateRecurrenceSummary();
    }

    private void ResetRecurringForm()
    {
        _isRecurring = false;
        _repeatFrequency = "Weekly";
        _selectedDate = DateTime.Today;
        RepeatLabel.Text = "Weekly";
        UpdateRepeatCheckmarks();
        DateLabel.Text = _selectedDate.ToString("MMM dd, yyyy");

        SimpleTab_Click(this, null!);
    }

    // ── Repeat Dropdown (Inline Card, Never Disappears) ────────────
    private void RepeatDropdownBtn_Click(object sender, MouseButtonEventArgs e)
    {
        // Toggle inline repeat options card
        RepeatOptionsCard.Visibility = (RepeatOptionsCard.Visibility == Visibility.Visible)
            ? Visibility.Collapsed
            : Visibility.Visible;

        // If calendar was open, collapse it to keep layout clean
        if (RepeatOptionsCard.Visibility == Visibility.Visible)
        {
            CalendarPopupCard.Visibility = Visibility.Collapsed;
            _isCalendarOpen = false;
        }
    }

    private void RepeatOption_Click(object sender, MouseButtonEventArgs e)
    {
        if (sender is Border b && b.Tag is string freq)
        {
            _repeatFrequency = freq;
            RepeatLabel.Text = freq;
            UpdateRepeatCheckmarks();
            UpdateRecurrenceSummary();
        }
        RepeatOptionsCard.Visibility = Visibility.Collapsed;
    }

    private void UpdateRepeatCheckmarks()
    {
        if (OptWeeklyText == null) return;
        OptWeeklyText.Text  = (_repeatFrequency == "Weekly")  ? "✓  Weekly"  : "    Weekly";
        OptMonthlyText.Text = (_repeatFrequency == "Monthly") ? "✓  Monthly" : "    Monthly";
        OptYearlyText.Text  = (_repeatFrequency == "Yearly")  ? "✓  Yearly"  : "    Yearly";
        OptDailyText.Text   = (_repeatFrequency == "Daily")   ? "✓  Daily"   : "    Daily";
    }

    private void UpdateRecurrenceSummary()
    {
        string dayName = _selectedDate.ToString("dddd");
        int dayNum = _selectedDate.Day;
        string suffix = (dayNum % 10 == 1 && dayNum != 11) ? "st" :
                        (dayNum % 10 == 2 && dayNum != 12) ? "nd" :
                        (dayNum % 10 == 3 && dayNum != 13) ? "rd" : "th";

        RecurrenceSummaryText.Text = _repeatFrequency switch
        {
            "Weekly"  => $"Repeats every week on {dayName}",
            "Monthly" => $"Repeats on the {dayNum}{suffix} of every month",
            "Yearly"  => $"Repeats every year on {_selectedDate:MMM dd}",
            "Daily"   => "Repeats every day",
            _         => $"Repeats {_repeatFrequency.ToLower()}"
        };
    }

    // ── Calendar Picker ────────────────────────────────────────────
    private void DateBtn_Click(object sender, MouseButtonEventArgs e)
    {
        _isCalendarOpen = !_isCalendarOpen;
        if (_isCalendarOpen)
        {
            // Collapse repeat dropdown if open
            RepeatOptionsCard.Visibility = Visibility.Collapsed;

            _calendarMonth = new DateTime(_selectedDate.Year, _selectedDate.Month, 1);
            RenderCalendar();
            CalendarPopupCard.Visibility = Visibility.Visible;
        }
        else
        {
            CalendarPopupCard.Visibility = Visibility.Collapsed;
        }
    }

    private void CalPrevMonth_Click(object sender, MouseButtonEventArgs e)
    {
        _calendarMonth = _calendarMonth.AddMonths(-1);
        RenderCalendar();
    }

    private void CalNextMonth_Click(object sender, MouseButtonEventArgs e)
    {
        _calendarMonth = _calendarMonth.AddMonths(1);
        RenderCalendar();
    }

    private void RenderCalendar()
    {
        CalMonthLabel.Text = _calendarMonth.ToString("MMMM yyyy");
        CalDaysGrid.Children.Clear();

        var firstDay = new DateTime(_calendarMonth.Year, _calendarMonth.Month, 1);
        int leadingBlanks = (int)firstDay.DayOfWeek;
        int daysInMonth = DateTime.DaysInMonth(_calendarMonth.Year, _calendarMonth.Month);

        // Blank cells before day 1
        for (int i = 0; i < leadingBlanks; i++)
        {
            CalDaysGrid.Children.Add(new Border());
        }

        // Days
        for (int d = 1; d <= daysInMonth; d++)
        {
            var date = new DateTime(_calendarMonth.Year, _calendarMonth.Month, d);
            bool isSelected = date.Date == _selectedDate.Date;
            bool isToday = date.Date == DateTime.Today;

            var dayCell = new Border
            {
                Width = 24,
                Height = 24,
                CornerRadius = new CornerRadius(12),
                Cursor = Cursors.Hand,
                Margin = new Thickness(1),
                Background = isSelected
                    ? new SolidColorBrush(Color.FromRgb(0x3B, 0x82, 0xF6))
                    : isToday
                        ? new SolidColorBrush(Color.FromArgb(0x30, 0xFF, 0xFF, 0xFF))
                        : Brushes.Transparent
            };

            var dayText = new TextBlock
            {
                Text = d.ToString(),
                FontSize = 10,
                FontFamily = new FontFamily("Segoe UI"),
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center,
                Foreground = isSelected ? Brushes.White : new SolidColorBrush(Color.FromArgb(0xDD, 0xFF, 0xFF, 0xFF))
            };

            dayCell.Child = dayText;
            dayCell.Tag = date;

            dayCell.MouseLeftButtonDown += (s, _) =>
            {
                if (s is Border clickedBorder && clickedBorder.Tag is DateTime dt)
                {
                    _selectedDate = dt;
                    DateLabel.Text = _selectedDate.ToString("MMM dd, yyyy");
                    UpdateRecurrenceSummary();
                    CalendarPopupCard.Visibility = Visibility.Collapsed;
                    _isCalendarOpen = false;
                }
            };

            // Subtle hover
            dayCell.MouseEnter += (s, _) =>
            {
                if (!isSelected && s is Border b)
                    b.Background = new SolidColorBrush(Color.FromArgb(0x20, 0xFF, 0xFF, 0xFF));
            };
            dayCell.MouseLeave += (s, _) =>
            {
                if (!isSelected && s is Border b)
                    b.Background = isToday ? new SolidColorBrush(Color.FromArgb(0x30, 0xFF, 0xFF, 0xFF)) : Brushes.Transparent;
            };

            CalDaysGrid.Children.Add(dayCell);
        }
    }

    // ── Confirm Add Task ───────────────────────────────────────────
    private void ConfirmAddBtn_Click(object sender, MouseButtonEventArgs e)
    {
        SubmitTask();
    }

    private void CancelBtn_Click(object sender, MouseButtonEventArgs e)
    {
        CloseAddPanel();
    }

    private void SubmitTask()
    {
        var title = TitleInput.Text?.Trim();
        if (string.IsNullOrWhiteSpace(title))
            return;

        if (_isRecurring)
        {
            var freq = _repeatFrequency switch
            {
                "Daily"   => RecurrenceType.Daily,
                "Weekly"  => RecurrenceType.Weekly,
                "Monthly" => RecurrenceType.Monthly,
                "Yearly"  => RecurrenceType.Yearly,
                _         => RecurrenceType.Weekly
            };

            var task = new TaskItem
            {
                Title = title,
                CreatedAt = DateTime.UtcNow,
                IsRecurring = true,
                RecurrenceType = freq,
                RecurrenceInterval = 1,
                RecurrenceStartDate = _selectedDate
            };

            try
            {
                var taskService = ((App)Application.Current).Services.GetService(typeof(Services.TaskService)) as Services.TaskService;
                taskService?.CreateTask(task);

                // If scheduled for today or earlier, generate occurrence immediately
                var recurringService = ((App)Application.Current).Services.GetService(typeof(Services.RecurringTaskService)) as Services.RecurringTaskService;
                recurringService?.GenerateRecurringTasks();

                _viewModel.LoadTasksCommand.Execute(null);
            }
            catch { }
        }
        else
        {
            _viewModel.NewTaskTitle = title;
            _viewModel.AddTaskCommand.Execute(null);
        }

        // Inline feedback
        InlineFeedback.Visibility = Visibility.Visible;
        TitleInput.Clear();

        var timer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(700) };
        timer.Tick += (s, _) =>
        {
            timer.Stop();
            InlineFeedback.Visibility = Visibility.Collapsed;
            CloseAddPanel();
            UpdateCount();
        };
        timer.Start();

        UpdateCount();
    }

    // ── Task Checkbox Click (Circular pop bounce animation + completed sinks to bottom) ──
    private void TaskCheckbox_Click(object sender, MouseButtonEventArgs e)
    {
        if (sender is Border b && b.Tag is TaskItemViewModel vm)
        {
            try
            {
                // 1. Tactile bounce animation on the circular tickbox (ensure fresh unfrozen transform)
                var scale = new ScaleTransform(1.0, 1.0);
                b.RenderTransform = scale;

                var bounceAnim = new DoubleAnimationUsingKeyFrames();
                bounceAnim.KeyFrames.Add(new SplineDoubleKeyFrame(1.35, KeyTime.FromTimeSpan(TimeSpan.FromMilliseconds(110)), new KeySpline(0.2, 0.8, 0.4, 1.0)));
                bounceAnim.KeyFrames.Add(new SplineDoubleKeyFrame(0.92, KeyTime.FromTimeSpan(TimeSpan.FromMilliseconds(210)), new KeySpline(0.2, 0.8, 0.4, 1.0)));
                bounceAnim.KeyFrames.Add(new SplineDoubleKeyFrame(1.0, KeyTime.FromTimeSpan(TimeSpan.FromMilliseconds(300)), new KeySpline(0.2, 0.8, 0.4, 1.0)));

                scale.BeginAnimation(ScaleTransform.ScaleXProperty, bounceAnim);
                scale.BeginAnimation(ScaleTransform.ScaleYProperty, bounceAnim);

                // 2. Pop checkmark canvas if completing
                if (b.Child is Canvas canvas)
                {
                    var markScale = new ScaleTransform(1.0, 1.0);
                    canvas.RenderTransform = markScale;

                    if (!vm.Task.IsCompleted)
                    {
                        var popMark = new DoubleAnimation(0.2, 1.0, TimeSpan.FromMilliseconds(200))
                        {
                            EasingFunction = new BackEase { Amplitude = 0.6, EasingMode = EasingMode.EaseOut }
                        };
                        markScale.BeginAnimation(ScaleTransform.ScaleXProperty, popMark);
                        markScale.BeginAnimation(ScaleTransform.ScaleYProperty, popMark);
                    }
                }
            }
            catch { }

            // 3. Toggle completion state in DB and ViewModel
            vm.ToggleCompleteCommand.Execute(null);
            UpdateCount();

            // 4. Give the bounce animation 320ms to celebrate before re-ordering list
            var timer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(320) };
            timer.Tick += (s, _) =>
            {
                timer.Stop();
                _viewModel.LoadTasksCommand.Execute(null);
                UpdateCount();
            };
            timer.Start();
        }
    }

    // ── Mouse Wheel Scrolling for Tasks ────────────────────────────
    private void TaskScroller_PreviewMouseWheel(object sender, MouseWheelEventArgs e)
    {
        if (sender is ScrollViewer scroller)
        {
            scroller.ScrollToVerticalOffset(scroller.VerticalOffset - (e.Delta / 3.0));
            e.Handled = true;
        }
    }

    // ── Delete Task ────────────────────────────────────────────────
    private void DeleteTask_Click(object sender, MouseButtonEventArgs e)
    {
        if (sender is Border b && b.Tag is TaskItemViewModel vm)
        {
            vm.DeleteCommand.Execute(null);
            _viewModel.LoadTasksCommand.Execute(null);
            UpdateCount();
        }
    }

    // ── Settings ───────────────────────────────────────────────────
    private void OpenSettings_Click(object sender, MouseButtonEventArgs e)
    {
        var settingsWindow = new SettingsWindow(_viewModel) { Owner = this };
        settingsWindow.ShowDialog();

        // Immediately update Topmost based on setting
        Topmost = _viewModel.Settings.AlwaysOnTop;

        if (_viewModel.Settings.StartWithWindows)
            _startupHelper.AddToStartup();
        else
            _startupHelper.RemoveFromStartup();
    }

    protected override void OnLocationChanged(EventArgs e)
    {
        base.OnLocationChanged(e);
        SaveWindowPlacement();
    }

    private void SaveWindowPlacement()
    {
        if (_viewModel?.Settings != null && Left >= 0 && Top >= 0)
        {
            _viewModel.Settings.WindowLeft = Left;
            _viewModel.Settings.WindowTop = Top;
            _viewModel.Settings.WindowWidth = Width;
        }
    }

    // ── Count Update ───────────────────────────────────────────────
    private void UpdateCount()
    {
        int count = _viewModel.Tasks.Count(t => !t.Task.IsCompleted);
        TaskCountText.Text = count.ToString();
    }
}
