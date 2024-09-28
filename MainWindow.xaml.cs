using System.Diagnostics;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
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
        this.MouseDown += delegate (object sender, MouseButtonEventArgs e) { if (e.ChangedButton == MouseButton.Left) DragMove(); };
    }
    
    private void ExitButton_Click(object sender, RoutedEventArgs e)
    {
        Environment.Exit(0);

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
                ApiButton.IsEnabled = false;
                ApiKey.IsEnabled = false;
                ToggleList.IsEnabled = true;
                Confirm.IsEnabled = true;
                Confirm.Content = "Click to move Entries.";
            }
        }
        else
        {
            //Move the Entries.
            bool toggle = ToggleList.IsChecked.Value;
            MoveLists(toggle);
        }
    }
    

    private async void MoveLists(bool direction)
    {
        MediaType originalType;
        MediaType newType;
        
        Random random = new Random();
        
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
        
        Confirm.IsEnabled = false;
        ToggleList.IsEnabled = false;
        
        var user = await aniClient.GetAuthenticatedUserAsync();

        var entryFilter = new MediaEntryFilter
        {
            Type = originalType,
        };

        int page = 0;
        
        var pagination = new AniPaginationOptions(page, 25);
        
        var mediaEntryCollection = await aniClient.GetUserEntryCollectionAsync(user.Id, originalType, pagination);

        if (mediaEntryCollection.Lists.Length <= 0)
        {
            Confirm.Width = 500;
            Confirm.Content = "No Entries found.";
            Confirm.Background = new SolidColorBrush(Colors.Coral);
            return;
        }
        
        List<MediaEntry> entries = new List<MediaEntry>();

        do
        {
            foreach (var list in mediaEntryCollection.Lists)
            {
                entries.AddRange(list.Entries);
            }

            if (mediaEntryCollection.HasNextChunk)
            {
                page++;
                pagination = new AniPaginationOptions(page, 25);
                mediaEntryCollection = await aniClient.GetUserEntryCollectionAsync(user.Id, originalType, pagination);
            }

        } while (mediaEntryCollection.HasNextChunk);

        if (entries.Count <= 0)
        {
            Confirm.Width = 500;
            Confirm.Content = "No Entries found.";
            Confirm.Background = new SolidColorBrush(Colors.Coral);
            return;
        }
        
        Progress.Visibility= Visibility.Visible;
        
        Confirm.Background = new SolidColorBrush(Colors.ForestGreen);
        
        Double progressPerEntry = 100.0 / entries.Count ;
        
        Double currentProgress = 0;
        
        int currentEntry = 0;
        

        List<string?> notFound = new List<string?>();

        foreach (var data in entries)
        {
            currentProgress = currentProgress + progressPerEntry;
            
            Progress.Value = currentProgress;
            
            Confirm.Content = $"Moving {entries.Count - currentEntry} Entries.";

            currentEntry++;
            
            int delayTime = random.Next(6000,7000);
            
            await Task.Delay(delayTime);
            
            if (data.Media.Type != originalType)
            {
                continue;
            }
            
            if (data.Status != MediaEntryStatus.Planning)
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
            
            var results = await aniClient.SearchMediaAsync(filter, new AniPaginationOptions(1, 20));

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
            else
            {
                await aniClient.DeleteMediaEntryAsync(data.Id);
                continue;
            }

            await aniClient.DeleteMediaEntryAsync(data.Id);
            
            if (newID == 0)
                continue;

            var mutation = new MediaEntryMutation
            {
                // Set properties of the mutation object as needed
                Status = MediaEntryStatus.Planning,
                Progress = 0
            };
            
            await aniClient.SaveMediaEntryAsync(newID,mutation);

        }
        
        Confirm.Content = $"Moved all Entries.";
        
    }
    
}