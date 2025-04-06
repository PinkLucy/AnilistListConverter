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

        ApiKey.IsEnabled = true;
        Confirm.Content = "Click to check Api Token";
        Confirm.IsEnabled = true;
    }

    private void ToggleList_OnClick(object sender, RoutedEventArgs e)
    {
        bool toggle = ToggleList.IsChecked.GetValueOrDefault();

        ToggleList.Content = toggle ? "Move Anime to Manga" : "Move Manga to Anime";
    }

    private async void ConfirmSettings_OnClick(object sender, RoutedEventArgs e)
    {
        if (!ToggleList.IsEnabled)
        {
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
            bool toggle = ToggleList.IsChecked.Value;
            MoveLists(toggle);
        }
    }

    private async void MoveLists(bool direction)
    {
        try
        {
            
            MediaType originalType = direction ? MediaType.Anime : MediaType.Manga;
            Logger.Log("OriginalType: " + originalType.ToString());
            MediaType newType = direction ? MediaType.Manga : MediaType.Anime;
            Random random = new Random();

            Confirm.IsEnabled = false;
            ToggleList.IsEnabled = false;
            
            if (!aniClient.IsAuthenticated)
            {
                Logger.Log("MainWindow.MoveLists: AniClient is not authenticated.");
                Confirm.Content = "AniList authentication failed. Please check your token.";
                Confirm.Background = new SolidColorBrush(Colors.Coral);
                return;
            }

            var user = await aniClient.GetAuthenticatedUserAsync();
            
            if (user == null)
            {
                Logger.Log("MainWindow.MoveLists: Authenticated user is null.");
                Confirm.Content = "Failed to fetch user info. Check your token.";
                Confirm.Background = new SolidColorBrush(Colors.Coral);
                return;
            }


            int page = 0;
            var pagination = new AniPaginationOptions(page, 25);
            
            if (pagination == null)
            {
                Logger.Log("Pagination object is null.");
                return;
            }

            Logger.Log($"Pagination pageIndex={pagination.PageIndex}, pageSize={pagination.PageSize}");


            MediaEntryCollection? mediaEntryCollection = null;

            try
            {
                Logger.Log($"Calling GetUserEntryCollectionAsync with userId={user.Id}, type={originalType}, page={page}");
                mediaEntryCollection = await aniClient.GetUserEntryCollectionAsync(user.Id, originalType, new AniPaginationOptions(page, 25));
            }
            catch (Exception ex)
            {
                Logger.Log("MoveLists: GetUserEntryCollectionAsync");
                return;
            }

            if (mediaEntryCollection.Lists.Length <= 0)
            {
                Confirm.Width = 500;
                Confirm.Content = "No Entries found.";
                Confirm.Background = new SolidColorBrush(Colors.Coral);
                return;
            }
            
            List<MediaEntry> unfilteredEntries = new();
            Logger.Log("Done adding, filtering...");
            do
            {
                foreach (var list in mediaEntryCollection.Lists)
                {
                    unfilteredEntries.AddRange(list.Entries);
                }
                await Task.Delay(random.Next(6000, 7000));
                if (mediaEntryCollection.HasNextChunk)
                {
                    page++;
                    pagination = new AniPaginationOptions(page, 25);
                    mediaEntryCollection = await aniClient.GetUserEntryCollectionAsync(user.Id, originalType, pagination);
                }
            } while (mediaEntryCollection.HasNextChunk);
            
            Logger.Log("Added UnfilteredEntries successfully.");

            if (unfilteredEntries.Count <= 0)
            {
                Confirm.Width = 500;
                Confirm.Content = "No Entries found.";
                Confirm.Background = new SolidColorBrush(Colors.Coral);
                return;
            }

            List<MediaEntry> entries = unfilteredEntries
                .Where(e => e.Media != null && e.Media.Type == originalType && e.Status == MediaEntryStatus.Planning)
                .ToList();
            
            Logger.Log("Created entries successfully.");
            
            Progress.Visibility = Visibility.Visible;
            Confirm.Background = new SolidColorBrush(Colors.ForestGreen);

            double progressPerEntry = 100.0 / entries.Count;
            double currentProgress = 0;
            int currentEntry = 0;

            List<string?> notFound = new();

            foreach (var data in entries)
            {
                try
                {
                    Logger.Log($"Moving {data.Media.Title.EnglishTitle}");
                    
                    currentProgress += progressPerEntry;
                    Progress.Value = currentProgress;
                    Confirm.Content = $"Moving {entries.Count - currentEntry} Entries.";
                    currentEntry++;

                    await Task.Delay(random.Next(6000, 7000));

                    var nativeTitle = data.Media.Title?.NativeTitle;
                    if (string.IsNullOrWhiteSpace(nativeTitle))
                    {
                        notFound.Add(null);
                        continue;
                    }

                    var filter = new SearchMediaFilter
                    {
                        Query = nativeTitle,
                        Type = newType,
                        Sort = MediaSort.Popularity
                    };

                    var results = await aniClient.SearchMediaAsync(filter, new AniPaginationOptions(1, 20));

                    int newID = 0;
                    if (results.TotalCount > 0)
                    {
                        foreach (var result in results.Data)
                        {
                            if (string.IsNullOrEmpty(result.Title?.NativeTitle))
                                continue;
                            Logger.Log($"Processing entry ID: {data.Id}, Title: {data.Media?.Title?.NativeTitle}, MediaType: {data.Media?.Type}");
                            int score = Fuzz.Ratio(result.Title.NativeTitle, nativeTitle);
                            if (score >= 85)
                            {
                                newID = result.Id;
                                break;
                            }
                        }
                    }
                    await Task.Delay(random.Next(6000, 7000));
                    await aniClient.DeleteMediaEntryAsync(data.Id);

                    if (newID == 0)
                        continue;

                    var mutation = new MediaEntryMutation
                    {
                        Status = MediaEntryStatus.Planning,
                        Progress = 0
                    };
                    await Task.Delay(random.Next(6000, 7000));
                    await aniClient.SaveMediaEntryAsync(newID, mutation);
                }
                catch (Exception ex)
                {
                    notFound.Add(data.Media?.Title?.NativeTitle ?? "Unknown");
                    Console.WriteLine($"Error processing entry {data.Id}: {ex.Message}");
                }
            }

            Confirm.Content = "Moved all Entries.";
        }
        catch (Exception ex)
        {
            Logger.Log($"MainWindow.MoveLists: {ex.Message}");
        }
    }
}
