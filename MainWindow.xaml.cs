using System.Diagnostics;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using AniListNet;
using AniListNet.Objects;
using AniListNet.Parameters;
using FuzzySharp;
using Process = System.Diagnostics.Process;

namespace AnilistListConverter;

/// <summary>
/// Interaction logic for MainWindow.xaml
/// </summary>
public partial class MainWindow
{
    private const string AuthUrl = "https://anilist.co/api/v2/oauth/authorize?client_id=21239&response_type=token";
    
    AniClient aniClient = new AniClient();
    
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
        Process.Start(new ProcessStartInfo
        {
            FileName = AuthUrl,
            UseShellExecute = true
        });

        // Enable the ApiKey TextBox
        ApiKey.IsEnabled = true;
        Confirm.Content = "Click to check Api Token";
        Confirm.IsEnabled = true;
    }
    
    private void ToggleList_OnClick(object sender, RoutedEventArgs e)
    {
        bool toggle = ToggleList.IsChecked.GetValueOrDefault();
        
        if (toggle)
        {
            ToggleList.Content = "Move Anime to Manga";
        }
        else
        {
            ToggleList.Content = "Move Manga to Anime";
        }
    }

    private async void ConfirmSettings_OnClick(object sender, RoutedEventArgs e)
    {
        if (!ToggleList.IsEnabled)
        {
            //Check the Api Key.
            var result = await aniClient.TryAuthenticateAsync(ApiKey.Text);
            if (!aniClient.IsAuthenticated)
            {
                Confirm.Width = 500;
                Confirm.Content = "Token rejected, please try again with a new one.";
                Confirm.Background = new SolidColorBrush(Colors.Coral);
            }
            else
            {
                ToggleList.IsEnabled = true;
                Confirm.IsEnabled = true;
                Confirm.Content = "Click to move Entries.";
            }
        }
        else
        {
            //Move the Entries.
            bool toggle = ToggleList.IsChecked.GetValueOrDefault();
            MoveLists(toggle);
        }
    }
    

    private async void MoveLists(bool direction)
    {
        MediaType originalType;
        MediaType newType;
        
        if (!direction)
        {
            originalType = MediaType.Manga;
            newType = MediaType.Anime;
        }
        else
        {
            originalType = MediaType.Anime;
            newType = MediaType.Manga;
        }
        
        var user = await aniClient.GetAuthenticatedUserAsync();
        
        var mediaEntries = aniClient.GetUserEntriesAsync(user.Id).Result;

        if (mediaEntries.Data.Length <= 0)
        {
            Confirm.Width = 500;
            Confirm.Content = "Could not find User Lists.";
            Confirm.Background = new SolidColorBrush(Colors.Coral);
            return;
        }
        
        Progress.Visibility= Visibility.Visible;

        // ReSharper disable once PossibleLossOfFraction
        Double progressPerEntry = 100 / mediaEntries.Data.Length ;
        
        Double currentProgress = 0;
        

        List<string?> notFound = new List<string?>();

        foreach (var data in mediaEntries.Data)
        {
            currentProgress = currentProgress + progressPerEntry;
            
            Progress.Value = currentProgress;
            
            if (data.Media.Type != originalType)
            {
                continue;
            }
            
            int newID = 0;
            
            var filter = new SearchMediaFilter
            {
                Query = data.Media.Title.NativeTitle,
                Type = newType,
                Sort = MediaSort.Popularity,
                
            };
            var results = aniClient.SearchMediaAsync(filter, new AniPaginationOptions(1, 20)).Result;

            if (results.TotalCount > 0)
            {
                foreach (var result in results.Data)
                {
                    int score = Fuzz.Ratio(result.Title.NativeTitle, data.Media.Title.NativeTitle);
                    
                    if (score >= 85)
                    {
                        newID = result.Id;
                        break;
                    }
                }
            }

            var mutation = new MediaEntryMutation
            {
                // Set properties of the mutation object as needed
                Status = MediaEntryStatus.Planning,
                Progress = 0
            };
            
            await aniClient.SaveMediaEntryAsync(newID,mutation);

        }
        
        
    }
    
}