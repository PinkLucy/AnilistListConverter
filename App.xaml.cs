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

        // Add a global exception handler
        AppDomain.CurrentDomain.UnhandledException += (sender, args) =>
        {
            Exception ex = (Exception)args.ExceptionObject;
            ShowErrorAndShutdown(ex);
        };

        // Also catch UI thread exceptions
        DispatcherUnhandledException += (sender, args) =>
        {
            Exception ex = args.Exception;
            args.Handled = true;
            ShowErrorAndShutdown(ex);
        };
    }

    private void ShowErrorAndShutdown(Exception ex)
    {
        MessageBox.Show(
            $"A fatal error occurred:\n\n{ex.Message}\n\n{ex.StackTrace}",
            "Unexpected Error",
            MessageBoxButton.OK,
            MessageBoxImage.Error);

        Current.Shutdown(); // more graceful than Environment.Exit
    }
}


