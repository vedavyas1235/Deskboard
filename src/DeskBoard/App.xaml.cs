using System;
using System.Threading;
using System.Windows;
using DeskBoard.Database;
using DeskBoard.Services;
using DeskBoard.ViewModels;
using Microsoft.Extensions.DependencyInjection;

namespace DeskBoard;

public partial class App : Application
{
    private static Mutex? _mutex;
    private static EventWaitHandle? _wakeupEvent;
    private const string MutexName = "Global\\DeskBoard_SingleInstance_Mutex_9C5E4790";
    private const string EventName = "Global\\DeskBoard_SingleInstance_Event_9C5E4790";

    private ServiceProvider? _serviceProvider;

    public IServiceProvider Services => _serviceProvider!;

    protected override void OnStartup(StartupEventArgs e)
    {
        // Enforce strict single-instance app: never allow duplicate taskbars
        _mutex = new Mutex(true, MutexName, out bool createdNew);
        if (!createdNew)
        {
            // Another instance is already running!
            // Signal the existing instance to wake up and focus, then exit immediately.
            try
            {
                using var wakeup = EventWaitHandle.OpenExisting(EventName);
                wakeup.Set();
            }
            catch { }

            Shutdown();
            return;
        }

        // Start listening for wake-up signals from any future clicks
        try
        {
            _wakeupEvent = new EventWaitHandle(false, EventResetMode.AutoReset, EventName);
            ThreadPool.QueueUserWorkItem(_ =>
            {
                while (_wakeupEvent.WaitOne())
                {
                    Current?.Dispatcher.Invoke(() =>
                    {
                        if (MainWindow is Views.MainWindow win)
                        {
                            win.WakeUpWindow();
                        }
                    });
                }
            });
        }
        catch { }

        base.OnStartup(e);

        ShutdownMode = ShutdownMode.OnMainWindowClose;

        // Add global exception handlers
        AppDomain.CurrentDomain.UnhandledException += (s, args) =>
        {
            var ex = args.ExceptionObject as Exception;
            System.Windows.MessageBox.Show(
                $"Fatal Error:\n{ex?.Message}\n\n{ex?.StackTrace}",
                "DeskBoard Error",
                System.Windows.MessageBoxButton.OK,
                System.Windows.MessageBoxImage.Error
            );
        };

        this.DispatcherUnhandledException += (s, args) =>
        {
            System.Windows.MessageBox.Show(
                $"Unexpected Error:\n{args.Exception?.Message}\n\n{args.Exception?.StackTrace}",
                "DeskBoard Error",
                System.Windows.MessageBoxButton.OK,
                System.Windows.MessageBoxImage.Error
            );
            args.Handled = true;
        };

        try
        {
            // Setup dependency injection
            var services = new ServiceCollection();
            ConfigureServices(services);
            _serviceProvider = services.BuildServiceProvider();

            // Initialize database
            var dbService = _serviceProvider.GetRequiredService<DatabaseService>();
            dbService.Initialize();

            // Start background recurring task monitor
            var recurringService = _serviceProvider.GetRequiredService<RecurringTaskService>();
            recurringService.Start();

            // Show main window
            var mainWindow = _serviceProvider.GetRequiredService<Views.MainWindow>();
            MainWindow = mainWindow;
            mainWindow.Show();
        }
        catch (Exception ex)
        {
            var msg = ex.InnerException != null ? $"{ex.Message}\n\nDetails: {ex.InnerException.Message}" : ex.Message;
            System.Windows.MessageBox.Show(
                $"Startup Error:\n{msg}",
                "DeskBoard Error",
                System.Windows.MessageBoxButton.OK,
                System.Windows.MessageBoxImage.Error
            );
        }
    }

    private void ConfigureServices(ServiceCollection services)
    {
        // Services
        services.AddSingleton<DatabaseService>();
        services.AddSingleton<TaskService>();
        services.AddSingleton<RecurringTaskService>();
        services.AddSingleton<NotificationService>();
        services.AddSingleton<SettingsService>();
        services.AddSingleton<DesktopIntegration.DesktopLayerService>();
        services.AddSingleton<BackupService>();

        // ViewModels
        services.AddSingleton<MainViewModel>();

        // Views
        services.AddSingleton<Views.MainWindow>();
    }

    protected override void OnExit(ExitEventArgs e)
    {
        try
        {
            _wakeupEvent?.Dispose();
            _mutex?.ReleaseMutex();
            _mutex?.Dispose();
        }
        catch { }

        _serviceProvider?.GetService<RecurringTaskService>()?.Stop();
        _serviceProvider?.Dispose();
        base.OnExit(e);
    }
}
