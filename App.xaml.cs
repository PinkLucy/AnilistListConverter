using System.Configuration;
using System.Data;
using System.Windows;

namespace AnilistListConverter;

/// <summary>
/// Interaction logic for App.xaml
/// </summary>
public partial class App : Application
{
    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        // Catch non-UI thread exceptions
        AppDomain.CurrentDomain.UnhandledException += (sender, args) =>
        {
            Exception ex = (Exception)args.ExceptionObject;
            LogUnhandledException("AppDomain.CurrentDomain.UnhandledException", ex);
        };

        // Catch UI thread exceptions
        DispatcherUnhandledException += (sender, args) =>
        {
            Exception ex = args.Exception;
            args.Handled = true;
            LogUnhandledException("Application.DispatcherUnhandledException", ex);
        };
    }

    private void LogUnhandledException(string source, Exception ex)
    {
        Console.WriteLine($"[UNHANDLED EXCEPTION] ({source})");
        Console.WriteLine($"Message: {ex.Message}");
        Console.WriteLine($"Stack Trace:\n{ex.StackTrace}");
        
    }
    
    
}
public static class Logger
{
    public static void LogException(string source, Exception ex)
    {
        Console.WriteLine($"[EXCEPTION] ({source})");
        Console.WriteLine($"Message: {ex.Message}");
        Console.WriteLine($"Stack Trace:\n{ex.StackTrace}");
    }
    public static void LogInfo(string message)
    {
        Console.WriteLine($"[INFO] {message}");
    }

}
