using System;
using System.IO;
using System.Windows;
using System.Windows.Threading;

namespace Ets2SaveEditor.App;

/// <summary>
/// Interaction logic for App.xaml
/// </summary>
public partial class App : Application
{
    protected override void OnStartup(StartupEventArgs e)
    {
        DispatcherUnhandledException += App_DispatcherUnhandledException;
        AppDomain.CurrentDomain.UnhandledException += CurrentDomain_UnhandledException;
        base.OnStartup(e);
    }

    private static void App_DispatcherUnhandledException(object sender, DispatcherUnhandledExceptionEventArgs e)
    {
        WriteCrashLog(e.Exception);
        // Keep the app alive so a theme glitch doesn't kill the whole editor.
        e.Handled = true;
    }

    private static void CurrentDomain_UnhandledException(object sender, UnhandledExceptionEventArgs e)
    {
        if (e.ExceptionObject is Exception ex)
            WriteCrashLog(ex);
    }

    private static void WriteCrashLog(Exception ex)
    {
        try
        {
            string path = Path.Combine(AppContext.BaseDirectory, "crash.log");
            File.AppendAllText(path, $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] {ex}\r\n\r\n");
        }
        catch { }
    }
}
