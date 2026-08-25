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
            LogFatalError("Dispatcher Unhandled Exception", ex.Exception);
            ex.Handled = true;
        };

        // Also catch Task exceptions
        System.Threading.Tasks.TaskScheduler.UnobservedTaskException += (s, ex) => {
            LogFatalError("Task Exception", ex.Exception);
            ex.SetObserved();
        };

        AppDomain.CurrentDomain.ProcessExit += (s, ex) => {
            try
            {
                Environment.Exit(0);
            }
            catch { }
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
            File.AppendAllText("crash_log.txt", $"\n[{DateTime.Now}] FATAL CRASH\n{message}\n");
        } catch { }

        MessageBox.Show(message, "OSRS Bridge Crash", MessageBoxButton.OK, MessageBoxImage.Error);
        Environment.Exit(1);
    }
}
