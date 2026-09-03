using System.Windows;
using System.IO;
using System;

namespace osrsmr;

public partial class App : Application
{
    protected override void OnStartup(StartupEventArgs e)
    {
        // Set current directory FIRST
        try {
            string? exePath = System.Environment.ProcessPath;
            if (!string.IsNullOrEmpty(exePath))
            {
                string directory = Path.GetDirectoryName(exePath) ?? "";
                if (!string.IsNullOrEmpty(directory))
                {
                    Directory.SetCurrentDirectory(directory);
                }
            }
        } catch { }

        // Immediate log for Admin mode
        try {
            string isElevated = new System.Security.Principal.WindowsPrincipal(System.Security.Principal.WindowsIdentity.GetCurrent()).IsInRole(System.Security.Principal.WindowsBuiltInRole.Administrator) ? "YES" : "NO";
            File.AppendAllText("startup_log.txt", $"[{DateTime.Now}] Starting app. Elevated: {isElevated}\n");
            File.AppendAllText("startup_log.txt", $"[{DateTime.Now}] Working Directory: {Directory.GetCurrentDirectory()}\n");
            File.AppendAllText("startup_log.txt", $"[{DateTime.Now}] Process Path: {System.Environment.ProcessPath}\n");
        } catch { }

        AppDomain.CurrentDomain.UnhandledException += (s, ex) => 
            LogFatalError("AppDomain Unhandled Exception", ex.ExceptionObject as Exception);
        
        DispatcherUnhandledException += (s, ex) => {
            try {
                string msg = $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] Dispatcher Exception: {ex.Exception?.Message}\n{ex.Exception?.StackTrace}\n";
                if (ex.Exception?.InnerException != null)
                {
                    msg += $"Inner Exception: {ex.Exception.InnerException.Message}\n{ex.Exception.InnerException.StackTrace}\n";
                }
                File.AppendAllText("error_log.txt", msg);
                File.AppendAllText("attach_log.txt", $"[DISPATCHER_EXCEPTION] {ex.Exception?.Message}\n");
            } catch { }
            
            // If main window hasn't loaded or isn't visible, this is a fatal startup crash
            if (MainWindow == null || !MainWindow.IsLoaded || !MainWindow.IsVisible)
            {
                LogFatalError("Startup Dispatcher Exception", ex.Exception);
            }
            else
            {
                ex.Handled = true;
            }
        };

        // Also catch Task exceptions and log without terminating app
        System.Threading.Tasks.TaskScheduler.UnobservedTaskException += (s, ex) => {
            try {
                string msg = $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] Unobserved Task Exception: {ex.Exception?.Message}\n{ex.Exception?.StackTrace}\n";
                File.AppendAllText("error_log.txt", msg);
                File.AppendAllText("attach_log.txt", $"[TASK_EXCEPTION] {ex.Exception?.Message}\n");
            } catch { }
            ex.SetObserved();
        };

        base.OnStartup(e);
    }

    protected override void OnExit(ExitEventArgs e)
    {
        try
        {
            Environment.Exit(0);
        }
        catch { }
        base.OnExit(e);
    }

    private void LogFatalError(string type, Exception? ex)
    {
        string message = $"{type}: {ex?.Message}\n\nStack Trace:\n{ex?.StackTrace}";
        if (ex?.InnerException != null)
        {
            message += $"\n\nInner Exception: {ex.InnerException.Message}\n{ex.InnerException.StackTrace}";
        }
        
        try {
            string baseDir = AppDomain.CurrentDomain.BaseDirectory;
            string crashLogPath = Path.Combine(baseDir, "crash_log.txt");
            string formatted = $"\n[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] FATAL CRASH\n{message}\n";
            File.AppendAllText(crashLogPath, formatted);
            File.AppendAllText("crash_log.txt", formatted);
            File.AppendAllText("error_log.txt", formatted);
            File.AppendAllText("attach_log.txt", $"[FATAL_CRASH] {type}: {ex?.Message}\n");
        } catch { }

        MessageBox.Show(message, "OSRS Bridge Crash", MessageBoxButton.OK, MessageBoxImage.Error);
        Environment.Exit(1);
    }
}
