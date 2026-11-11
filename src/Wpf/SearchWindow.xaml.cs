using System.IO;
using System.Windows;
using System.Windows.Input;

namespace Xplorer;

public partial class SearchWindow : Window
{
    private readonly string root;
    private readonly Func<string, Task> openLocation;
    private CancellationTokenSource? search;
    private bool closed;

    internal SearchWindow(string root, Func<string, Task> openLocation)
    {
        InitializeComponent();
        this.root = root;
        this.openLocation = openLocation;
        Scope.Text = $"Source: local folder search · Scope: {root} (including subfolders)";
        Loaded += (_, _) => Query.Focus();
        Closed += (_, _) => { closed = true; search?.Cancel(); };
    }

    private async void Search_Click(object sender, RoutedEventArgs e) => await RunSearchAsync();
    private async void Query_KeyDown(object sender, KeyEventArgs e) { if (e.Key == Key.Enter) await RunSearchAsync(); }
    private void Stop_Click(object sender, RoutedEventArgs e) => search?.Cancel();

    private async Task RunSearchAsync()
    {
        string query = Query.Text.Trim();
        if (query.Length == 0) { SearchStatus.Text = "Enter a name to search for."; return; }
        search?.Cancel();
        var request = new CancellationTokenSource();
        search = request;
        Results.ItemsSource = null;
        SearchStatus.Text = "Searching …";
        try
        {
            var results = await Task.Run(() => LocalSearch.Find(root, query, request.Token), request.Token);
            if (request.IsCancellationRequested || closed) return;
            Results.ItemsSource = results.Items;
            SearchStatus.Text = $"{results.Items.Count} result(s)." + (results.LimitReached ? " Limit of 1,000 reached; narrow your search." : "") + (results.SkippedFolders > 0 ? $" {results.SkippedFolders} folder(s) could not be searched." : "") + " Linked folders are not followed.";
        }
        catch (OperationCanceledException) { if (!closed && ReferenceEquals(search, request)) SearchStatus.Text = "Search canceled."; }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or ArgumentException or System.Security.SecurityException)
        { if (!closed && ReferenceEquals(search, request)) SearchStatus.Text = $"Search failed: {ex.Message}"; }
        finally { if (ReferenceEquals(search, request)) search = null; request.Dispose(); }
    }

    private async void OpenLocation_Click(object sender, RoutedEventArgs e) => await OpenSelectedAsync();
    private async void Results_DoubleClick(object sender, MouseButtonEventArgs e) => await OpenSelectedAsync();
    private async Task OpenSelectedAsync()
    {
        if (Results.SelectedItem is not SearchResult result) return;
        string? parent = Path.GetDirectoryName(result.Path);
        if (parent is null) return;
        Close();
        await openLocation(parent);
    }
}
