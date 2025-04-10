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
    
    int ratelimit = 3000;

    public MainWindow()
    {
        InitializeComponent();
        aniClient.RateChanged += ChangeRatelimit;
        ratelimit = (60000 / 30) + 1;
        this.MouseDown += delegate (object sender, MouseButtonEventArgs e) { if (e.ChangedButton == MouseButton.Left) DragMove(); };
    }
    
    private void ExitButton_Click(object sender, RoutedEventArgs e)
    {
        Environment.Exit(0);
    }
    public void ChangeRatelimit(object? sender, AniRateEventArgs e)
    {
        int oldRate = ratelimit;
        ratelimit = (60000 / (e.RateLimit - 60)) + 1;
        if (oldRate != ratelimit)
        {
            LogBox.Text += "\n" + "New Rate limit. One API Call every " + ratelimit + "ms";
        }
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
            LogBox.Text += "\n" +  "OriginalType: " + originalType.ToString();
            LogBox.ScrollToEnd();
            MediaType newType = direction ? MediaType.Manga : MediaType.Anime;
            Random random = new Random();

            Confirm.IsEnabled = false;
            ToggleList.IsEnabled = false;
            
            if (!aniClient.IsAuthenticated)
            {
                LogBox.Text += "\n" +  "MainWindow.MoveLists: AniClient is not authenticated.";
                LogBox.ScrollToEnd();
                Confirm.Content = "AniList authentication failed. Please check your token.";
                Confirm.Background = new SolidColorBrush(Colors.Coral);
                return;
            }

            await Task.Delay(ratelimit);

            var user = await aniClient.GetAuthenticatedUserAsync();
            
            if (user == null)
            {
                LogBox.Text += "\n" + "MainWindow.MoveLists: Authenticated user is null.";
                LogBox.ScrollToEnd();
                Confirm.Content = "Failed to fetch user info. Check your token.";
                Confirm.Background = new SolidColorBrush(Colors.Coral);
                return;
            }


            int page = 0;
            var pagination = new AniPaginationOptions(page, 25);
            
            if (pagination == null)
            {
                LogBox.Text += "\n" + "Pagination object is null.";
                LogBox.ScrollToEnd();
                return;
            }

            LogBox.Text += "\n" + $"Pagination pageIndex={pagination.PageIndex}, pageSize={pagination.PageSize}";
            LogBox.ScrollToEnd();

            MediaEntryCollection? mediaEntryCollection = null;

            try
            {
                LogBox.Text += "\n" + $"Calling GetUserEntryCollectionAsync with userId={user.Id}, type={originalType}, page={page}";
                LogBox.ScrollToEnd();
                await Task.Delay(ratelimit);
                mediaEntryCollection = await aniClient.GetUserEntryCollectionAsync(user.Id, originalType, new AniPaginationOptions(page, 25));
            }
            catch (Exception ex)
            {
                LogBox.Text += "\n" + "MoveLists: GetUserEntryCollectionAsync";
                LogBox.ScrollToEnd();
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
            LogBox.Text += "\n" + "Done adding, filtering...";
            LogBox.ScrollToEnd();
            do
            {
                foreach (var list in mediaEntryCollection.Lists)
                {
                    unfilteredEntries.AddRange(list.Entries);
                }
                await Task.Delay(ratelimit);
                if (mediaEntryCollection.HasNextChunk)
                {
                    page++;
                    pagination = new AniPaginationOptions(page, 25);
                    mediaEntryCollection = await aniClient.GetUserEntryCollectionAsync(user.Id, originalType, pagination);
                }
            } while (mediaEntryCollection.HasNextChunk);
            
            LogBox.Text += "\n" + "Added UnfilteredEntries successfully.";
            LogBox.ScrollToEnd();

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
            
            LogBox.Text += "\n" + "Created entries successfully.";
            LogBox.ScrollToEnd();
            
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
                    LogBox.Text += "\n" + $"Moving {data.Media.Title.EnglishTitle}";
                    LogBox.ScrollToEnd();
                    
                    currentProgress += progressPerEntry;
                    Progress.Value = currentProgress;
                    Confirm.Content = $"Moving {entries.Count - currentEntry} Entries.";
                    currentEntry++;

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
                    
                    await Task.Delay(ratelimit);

                    var results = await aniClient.SearchMediaAsync(filter, new AniPaginationOptions(1, 20));

                    int newID = 0;
                    if (results.TotalCount > 0)
                    {
                        foreach (var result in results.Data)
                        {
                            if (string.IsNullOrEmpty(result.Title?.NativeTitle))
                                continue;
                            int score = Fuzz.Ratio(result.Title.NativeTitle, nativeTitle);
                            if (score >= 85)
                            {
                                newID = result.Id;
                                break;
                            }
                        }
                    }
                    await Task.Delay(ratelimit);
                    await aniClient.DeleteMediaEntryAsync(data.Id);

                    if (newID == 0)
                        continue;

                    var mutation = new MediaEntryMutation
                    {
                        Status = MediaEntryStatus.Planning,
                        Progress = 0
                    };
                    await Task.Delay(ratelimit);
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
            LogBox.Text += "\n" + $"MainWindow.MoveLists: {ex.Message}";
            LogBox.ScrollToEnd();
        }
    }
}
