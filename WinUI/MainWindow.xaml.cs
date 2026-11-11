using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Windows.System;
using Windows.UI.Core;
using Microsoft.UI.Input;
using Microsoft.UI.Xaml.Media;

namespace Xplorer.WinUI;

public sealed partial class MainWindow : Window
{
    private const string DrivesPath = "xplorer:drives";
    private sealed class TabState
    {
        public List<string> History { get; } = [];
        public int Index { get; set; } = -1;
        public string? Path { get; set; }
    }
    private readonly FavoritesStore favoriteStore = new(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Xplorer", "favorites.json"));
    private readonly List<string> favorites = [];
    private readonly ObservableCollection<FolderItem> items = [];
    private readonly SemaphoreSlim sizeWorkers = new(2);
    private CancellationTokenSource? load;
    private CancellationTokenSource? sizes;
    private CancellationTokenSource? search;
    private TabState? active;
    private string sortColumn = "Name";
    private bool descending;
    private bool searching;
    private bool favoritesAvailable = true;
    private bool fileOperationRunning;
    private FileSystemWatcher? watcher;
    private int watcherVersion;
    private readonly DispatcherTimer refreshTimer = new() { Interval = TimeSpan.FromMilliseconds(600) };
    private readonly DispatcherTimer sortTimer = new() { Interval = TimeSpan.FromMilliseconds(300) };

    public MainWindow()
    {
        InitializeComponent();
        Files.ItemsSource = items;
        AppWindow.Closing += (_, e) => { if (fileOperationRunning) { e.Cancel = true; Status.Text = "Wait for the file operation to finish or cancel it in the Windows dialog."; } };
        Closed += (_, _) => { watcherVersion++; watcher?.Dispose(); refreshTimer.Stop(); load?.Cancel(); sizes?.Cancel(); search?.Cancel(); };
        sortTimer.Tick += (_, _) => { sortTimer.Stop(); if (sortColumn == "Size") SetItems(items.ToList(), true); };
        refreshTimer.Tick += async (_, _) => { refreshTimer.Stop(); if (fileOperationRunning || load is not null) { refreshTimer.Start(); return; } if (active?.Path is { } path && !searching) await NavigateAsync(path, active.Index); };
        try { favorites.AddRange(favoriteStore.Load()); RenderFavorites(); }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or System.Text.Json.JsonException) { favoritesAvailable = false; Status.Text = $"Could not load favorites; existing data will not be overwritten: {ex.Message}"; }
        foreach (var place in SidebarLocations.GetPlaces()) AddLocation(Places, place.Name, place.Path);
        _ = LoadDrivesAsync();
        AddTab();
    }

    private async Task LoadDrivesAsync()
    {
        try
        {
            var drives = await Task.Run(SidebarLocations.GetDrives);
            Drives.Children.Clear();
            foreach (var drive in drives) AddLocation(Drives, drive.Name, drive.Path);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException) { Status.Text = $"Could not list drives: {ex.Message}"; }
    }
    private async void RefreshDrives_Click(object sender, RoutedEventArgs e) => await LoadDrivesAsync();
    private void AddLocation(StackPanel panel, string name, string path)
    {
        var button = new Button { Content = name, HorizontalAlignment = HorizontalAlignment.Stretch, HorizontalContentAlignment = HorizontalAlignment.Left, Tag = path };
        ToolTipService.SetToolTip(button, path);
        button.Click += async (_, _) => await NavigateAsync(path);
        panel.Children.Add(button);
    }
    private void RenderFavorites()
    {
        Favorites.Children.Clear();
        foreach (string path in favorites)
        {
            var row = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 4 };
            string name = Path.GetFileName(Path.TrimEndingDirectorySeparator(path));
            var open = new Button { Content = string.IsNullOrEmpty(name) ? path : name, MaxWidth = 140 };
            ToolTipService.SetToolTip(open, path);
            open.Click += async (_, _) => await NavigateAsync(path);
            var remove = new Button { Content = "×" };
            remove.Click += (_, _) => SaveFavorites(favorites.Where(x => !string.Equals(x, path, StringComparison.OrdinalIgnoreCase)).ToList());
            row.Children.Add(open); row.Children.Add(remove); Favorites.Children.Add(row);
        }
    }
    private void SaveFavorites(List<string> next)
    {
        if (!favoritesAvailable) { Status.Text = "Favorites could not be loaded and cannot be changed in this session."; return; }
        try { favoriteStore.Save(next); favorites.Clear(); favorites.AddRange(next); RenderFavorites(); }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException) { Status.Text = $"Could not save favorites: {ex.Message}"; }
    }
    private void AddFavorite_Click(object sender, RoutedEventArgs e)
    {
        if (active?.Path is not { } path || path == DrivesPath) return;
        if (!favorites.Contains(path, StringComparer.OrdinalIgnoreCase)) SaveFavorites([.. favorites, path]);
    }
    private void AddTab()
    {
        var tab = new TabViewItem { Header = "New tab", Tag = new TabState(), IsClosable = false };
        tab.PointerPressed += (_, e) => { if (e.GetCurrentPoint(tab).Properties.IsMiddleButtonPressed && Tabs.TabItems.Count > 1) { Tabs.TabItems.Remove(tab); e.Handled = true; } };
        Tabs.TabItems.Add(tab);
        Tabs.SelectedItem = tab;
    }
    private void AddTab_Click(TabView sender, object args) => AddTab();
    private void TabClose_Click(TabView sender, TabViewTabCloseRequestedEventArgs args)
    {
        if (Tabs.TabItems.Count > 1) Tabs.TabItems.Remove(args.Tab);
    }
    private async void TabSelection_Changed(object sender, SelectionChangedEventArgs e)
    {
        if (Tabs.SelectedItem is not TabViewItem { Tag: TabState tab } || ReferenceEquals(active, tab)) return;
        load?.Cancel(); sizes?.Cancel(); search?.Cancel(); active = tab;
        await NavigateAsync(tab.Path ?? Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), tab.Index >= 0 ? tab.Index : null);
    }
    private async Task NavigateAsync(string path, int? index = null)
    {
        if (active is null) return;
        load?.Cancel(); sizes?.Cancel(); search?.Cancel(); searching = false;
        var request = new CancellationTokenSource(); load = request;
        var tab = active;
        Status.Text = "Loading folder …";
        try
        {
            var result = await Task.Run(() => path == DrivesPath
                ? new FolderContents(DrivesPath, SidebarLocations.GetDrives().Select(d => new FolderItem(d.Name, d.Path, true, "Drive", null, DateTime.MinValue)).ToList())
                : FolderReader.Read(path, request.Token), request.Token);
            if (request.IsCancellationRequested || !ReferenceEquals(tab, active)) return;
            tab.Path = result.Path;
            if (index is int target) tab.Index = target;
            else if (tab.Index < 0 || !string.Equals(tab.History[tab.Index], result.Path, StringComparison.OrdinalIgnoreCase))
            { tab.History.RemoveRange(tab.Index + 1, tab.History.Count - tab.Index - 1); tab.History.Add(result.Path); tab.Index = tab.History.Count - 1; }
            if (Tabs.SelectedItem is TabViewItem selected) selected.Header = result.Path == DrivesPath ? "Drives" : Path.GetFileName(Path.TrimEndingDirectorySeparator(result.Path)) is { Length: > 0 } name ? name : result.Path;
            Address.Text = result.Path;
            RenderBreadcrumbs(result.Path);
            SetItems(result.Items, index is not null && string.Equals(tab.Path, result.Path, StringComparison.OrdinalIgnoreCase));
            Status.Text = $"{items.Count} items";
            StartWatching(result.Path);
            if (path != DrivesPath && FolderSizes.IsChecked == true) _ = CalculateSizesAsync(result.Items);
        }
        catch (OperationCanceledException) { }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or ArgumentException or NotSupportedException or System.Security.SecurityException)
        { if (!request.IsCancellationRequested) Status.Text = $"Could not open folder: {ex.Message}"; }
        finally { if (ReferenceEquals(load, request)) load = null; request.Dispose(); }
    }
    private async void StartWatching(string path)
    {
        if (watcher is not null && string.Equals(watcher.Path, path, StringComparison.OrdinalIgnoreCase)) return;
        int version = ++watcherVersion; watcher?.Dispose(); watcher = null; refreshTimer.Stop();
        if (path == DrivesPath) return;
        FileSystemWatcher? next = null;
        try
        {
            next = await Task.Run(() => new FileSystemWatcher(path) { NotifyFilter = NotifyFilters.FileName | NotifyFilters.DirectoryName | NotifyFilters.Size | NotifyFilters.LastWrite });
            if (version != watcherVersion) { next.Dispose(); return; }
            FileSystemEventHandler changed = (s, _) => DispatcherQueue.TryEnqueue(() => { if (ReferenceEquals(s, watcher) && !searching) { refreshTimer.Stop(); refreshTimer.Start(); } });
            next.Changed += changed; next.Created += changed; next.Deleted += changed; next.Renamed += (s, e) => changed(s, e);
            next.Error += (s, e) => DispatcherQueue.TryEnqueue(() => { if (ReferenceEquals(s, watcher)) { watcher?.Dispose(); watcher = null; Status.Text = $"Automatic refresh unavailable: {e.GetException().Message}"; } });
            watcher = next;
            await Task.Run(() => next.EnableRaisingEvents = true);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or ArgumentException or InvalidOperationException or ObjectDisposedException)
        { if (ReferenceEquals(watcher, next)) watcher = null; next?.Dispose(); if (version == watcherVersion) Status.Text = $"Automatic refresh unavailable: {ex.Message}"; }
    }
    private void RenderBreadcrumbs(string path)
    {
        Breadcrumbs.Children.Clear();
        if (path == DrivesPath) return;
        foreach (var crumb in BreadcrumbBuilder.Build(path))
        {
            var button = new Button { Content = crumb.Name };
            button.Click += async (_, _) => await NavigateAsync(crumb.Path);
            Breadcrumbs.Children.Add(button);
        }
    }
    private void SetItems(IEnumerable<FolderItem> entries, bool preserveSelection = false)
    {
        var selected = preserveSelection ? Files.SelectedItems.OfType<FolderItem>().Select(x => x.Path).ToHashSet(StringComparer.OrdinalIgnoreCase) : null;
        var sorted = entries.ToList();
        sorted.Sort((a, b) => new FolderItemComparer(sortColumn, descending).Compare(a, b));
        items.Clear(); foreach (var item in sorted) items.Add(item);
        if (selected is not null) foreach (var item in sorted.Where(x => selected.Contains(x.Path))) Files.SelectedItems.Add(item);
    }
    private async Task CalculateSizesAsync(List<FolderItem> folders)
    {
        sizes?.Cancel(); var request = new CancellationTokenSource(); sizes = request;
        try
        {
            foreach (var item in folders.Where(x => x.IsDirectory)) item.Size = "Calculating …";
            await Parallel.ForEachAsync(folders.Where(x => x.IsDirectory), new ParallelOptions { MaxDegreeOfParallelism = 2, CancellationToken = request.Token }, async (item, token) =>
            {
                await sizeWorkers.WaitAsync(token);
                string text; string detail = ""; long? bytes = null;
                try { var result = await Task.Run(() => FolderSizeReader.Read(item.Path, token), token); bytes = result.Bytes; text = $"{result.Bytes:N0} B"; if (result.SkippedLinks > 0) { text += " *"; detail = $"Skipped {result.SkippedLinks} symbolic link(s) or junction(s)."; } }
                catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or System.Security.SecurityException or OverflowException) { text = "Unavailable"; detail = ex.Message; }
                finally { sizeWorkers.Release(); }
                DispatcherQueue.TryEnqueue(() => { if (!token.IsCancellationRequested) { item.SizeBytes = bytes; item.Size = text; item.SizeDetail = detail; if (sortColumn == "Size" && !sortTimer.IsEnabled) sortTimer.Start(); } });
            });
        }
        catch (OperationCanceledException) { }
        finally { if (ReferenceEquals(sizes, request)) sizes = null; request.Dispose(); }
    }
    private async void Search_Click(object sender, RoutedEventArgs e)
    {
        if (load is not null) { Status.Text = "Wait for the folder to finish loading before searching."; return; }
        if (active?.Path is not { } path || path == DrivesPath || string.IsNullOrWhiteSpace(SearchQuery.Text)) return;
        search?.Cancel(); sizes?.Cancel(); var request = new CancellationTokenSource(); search = request;
        var tab = active; string query = SearchQuery.Text.Trim(); searching = true;
        Status.Text = $"Searching locally in {path} …";
        try
        {
            var result = await Task.Run(() => LocalSearch.Find(path, query, request.Token), request.Token);
            if (request.IsCancellationRequested || !ReferenceEquals(tab, active)) return;
            SetItems(result.Items.Select(x => new FolderItem(x.Name, x.Path, x.IsDirectory, x.IsDirectory ? "Folder" : Path.GetExtension(x.Name), null, DateTime.MinValue)));
            Status.Text = $"Local search in {path}: {result.Items.Count} result(s)." + (result.LimitReached ? " Limit reached." : "") + $" {result.SkippedFolders} folder(s) skipped. Linked folders are not followed.";
        }
        catch (OperationCanceledException) { if (ReferenceEquals(search, request)) Status.Text = "Search canceled."; }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or ArgumentException) { Status.Text = $"Search failed: {ex.Message}"; }
        finally { if (ReferenceEquals(search, request)) search = null; request.Dispose(); }
    }
    private void StopSearch_Click(object sender, RoutedEventArgs e) => search?.Cancel();
    private async void Drives_Click(object sender, RoutedEventArgs e) => await NavigateAsync(DrivesPath);
    private async void Refresh_Click(object sender, RoutedEventArgs e) { if (active?.Path is { } path) await NavigateAsync(path, active.Index); }
    private async void OpenAddress_Click(object sender, RoutedEventArgs e) => await NavigateAsync(Address.Text);
    private async void Address_KeyDown(object sender, KeyRoutedEventArgs e) { if (e.Key == VirtualKey.Enter) await NavigateAsync(Address.Text); }
    private async void Back_Click(object sender, RoutedEventArgs e) { if (active is { Index: > 0 } tab) await NavigateAsync(tab.History[tab.Index - 1], tab.Index - 1); }
    private async void Forward_Click(object sender, RoutedEventArgs e) { if (active is { } tab && tab.Index + 1 < tab.History.Count) await NavigateAsync(tab.History[tab.Index + 1], tab.Index + 1); }
    private async void Up_Click(object sender, RoutedEventArgs e) { if (active?.Path is not { } path || path == DrivesPath) return; await NavigateAsync(Directory.GetParent(path)?.FullName ?? DrivesPath); }
    private void Sort_Click(object sender, RoutedEventArgs e) { if (sender is Button { Tag: string column }) { descending = sortColumn == column && !descending; sortColumn = column; SetItems(items.ToList(), true); } }
    private async void FolderSizes_Click(object sender, RoutedEventArgs e) { if (active?.Path is { } path) await NavigateAsync(path, active.Index); }
    private async void Files_DoubleTapped(object sender, DoubleTappedRoutedEventArgs e)
    {
        if (Files.SelectedItem is not FolderItem item) return;
        if (item.IsDirectory) await NavigateAsync(item.Path);
        else try { Process.Start(new ProcessStartInfo(item.Path) { UseShellExecute = true }); }
        catch (Exception ex) when (ex is System.ComponentModel.Win32Exception or InvalidOperationException or IOException) { Status.Text = $"Could not open file: {ex.Message}"; }
    }
}
