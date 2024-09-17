using System.Diagnostics;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;

namespace AnilistListConverter;

/// <summary>
/// Interaction logic for MainWindow.xaml
/// </summary>
public partial class MainWindow : Window
{
    private const string ClientId = "21239"; // Replace with your actual client ID
    private const string AuthUrlTemplate = "https://anilist.co/api/v2/oauth/authorize?client_id={0}&response_type=token";

    private State currentState = State.PreApi;

    private enum State
    {
        PreApi,
        ApiRequested,
        ApiFailed,
        ApiSuccess,
        Working
    }

    
    public MainWindow()
    {
        InitializeComponent();
    }
    
    private void ExitButton_Click(object sender, RoutedEventArgs e)
    {
        Application.Current.Shutdown();
    }

    private void ApiButton_OnClick(object sender, RoutedEventArgs e)
    {
        string authUrl = string.Format(AuthUrlTemplate, ClientId);
        Process.Start(new ProcessStartInfo
        {
            FileName = authUrl,
            UseShellExecute = true
        });

        // Enable the ApiKey TextBox
        ApiKey.IsEnabled = true;
        Confirm.Content = "Click to check Api Token";
        Confirm.IsEnabled = true;
        currentState = State.ApiRequested;
    }

    private void ConfirmSettings_OnClick(object sender, RoutedEventArgs e)
    {
        Color color = (Color)ColorConverter.ConvertFromString("#8da888");
        switch (currentState)
        {
            case State.ApiRequested:
                Confirm.Width = 300;
                Confirm.Background = new SolidColorBrush(color);
                
                
                break;
            case State.ApiFailed:
                Confirm.Width = 500;
                Confirm.Content = "Token rejected, please try again with a new one.";
                Confirm.Background = new SolidColorBrush(Colors.Coral);
                break;
            case State.ApiSuccess:
                Confirm.Width = 300;
                Confirm.Background = new SolidColorBrush(color);
                break;
            case State.Working:
                Confirm.Width = 500;
                Confirm.Background = new SolidColorBrush(Colors.Green);
                break;
            case State.PreApi:
                Confirm.Width = 500;
                Confirm.Content = "Something went horribly wrong, restart the program.";
                Confirm.IsEnabled = false;
                Confirm.Background = new SolidColorBrush(Colors.Red);
                ApiButton.IsEnabled = false;
                ApiKey.IsEnabled = false;
                ToggleList.IsEnabled = false;
                TogglePlannedAll.IsEnabled = false;
                break;
        }
    }
}