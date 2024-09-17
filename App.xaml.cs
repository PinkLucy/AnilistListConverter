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
        // Display the error in a message box
        MessageBox.Show($"An unexpected error occurred: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);

        // Close the app after the user clicks OK
        Environment.Exit(1);
    }
}

