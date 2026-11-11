using System.Diagnostics;
using System.IO;
using System.Windows;
using System.Windows.Input;
using System.Windows.Controls;
using System.Runtime.InteropServices;
using System.Windows.Data;
using System.Windows.Threading;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Collections.Specialized;

namespace Xplorer;

public partial class MainWindow : Window
{
    [DllImport("kernel32.dll", EntryPoint = "CreateDirectoryW", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern bool CreateDirectoryNative(string path, IntPtr securityAttributes);
    [DllImport("user32.dll")]
    private static extern uint GetClipboardSequenceNumber();
    private const string DrivesPath = "xplorer:drives";
    private sealed class TabState : INotifyPropertyChanged
    {
        public List<string> History { get; } = [];
        public int HistoryIndex { get; set; } = -1;
        public string? Path { get; set; }
        private string title = "New tab";
        public string Title { get => title; set { title = value; PropertyChanged?.Invoke(this, new(nameof(Title))); } }
        public event PropertyChangedEventHandler? PropertyChanged;
    }
    private readonly ObservableCollection<TabState> tabs = [];
    private TabState activeTab = new();
    private List<string> history => activeTab.History;
    private int historyIndex { get => activeTab.HistoryIndex; set => activeTab.HistoryIndex = value; }
    private CancellationTokenSource? loading;
    private CancellationTokenSource? sizeLoading;
    private readonly SemaphoreSlim sizeWorkers = new(2);
    private string? currentPath { get => activeTab.Path; set => activeTab.Path = value; }
    private int driveRefreshVersion;
    private string sortColumn = "Name";
    private bool sortDescending;
    private readonly DispatcherTimer sortTimer = new() { Interval = TimeSpan.FromMilliseconds(300) };
    private readonly DispatcherTimer refreshTimer = new() { Interval = TimeSpan.FromMilliseconds(600) };
    private FileSystemWatcher? folderWatcher;
    private int watcherVersion;
    private bool windowClosed;
    private bool fileOperationRunning;
    private readonly FavoritesStore favoritesStore = new(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Xplorer", "favorites.json"));
    private readonly ObservableCollection<SidebarLocation> favorites = [];
    private bool favoritesAvailable = true;

    public MainWindow()
    {
        InitializeComponent();
        sortTimer.Tick += (_, _) => { sortTimer.Stop(); ApplySort(); };
        refreshTimer.Tick += async (_, _) =>
        {
            refreshTimer.Stop();
            if (loading is not null || fileOperationRunning) { refreshTimer.Start(); return; }
            await RefreshCurrentAsync();
        };
        Closing += (_, e) =>
        {
            if (fileOperationRunning) { e.Cancel = true; Status.Text = "Wait for the file operation to finish or cancel it in the Windows dialog."; }
        };
        Loaded += async (_, _) =>
        {
            _ = LoadPlacesAsync();
            _ = RefreshDrivesAsync();
            FavoritesList.ItemsSource = favorites;
            LoadFavorites();
            Tabs.ItemsSource = tabs;
            tabs.Add(activeTab);
            Tabs.SelectedItem = activeTab;
            await NavigateAsync(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile));
        };
        Closed += (_, _) => { windowClosed = true; watcherVersion++; refreshTimer.Stop(); folderWatcher?.Dispose(); loading?.Cancel(); sizeLoading?.Cancel(); };
    }

    private void LoadFavorites()
    {
        try { foreach (var path in favoritesStore.Load()) favorites.Add(FavoriteLocation(path)); }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or System.Text.Json.JsonException or System.Security.SecurityException)
        { favoritesAvailable = false; ShowCopyableDetails($"Could not load favorites. Existing favorites will not be overwritten: {ex.Message}"); }
    }

    private static SidebarLocation FavoriteLocation(string path)
    {
        string name = Path.GetFileName(Path.TrimEndingDirectorySeparator(path));
        return new(string.IsNullOrEmpty(name) ? path : name, path);
    }

    private void AddFavorite_Click(object sender, RoutedEventArgs e)
    {
        if (currentPath is null or DrivesPath) { Status.Text = "Open a folder first."; return; }
        AddFavorite(currentPath);
    }

    private void AddFavorite(string path)
    {
        if (!favoritesAvailable) { ShowCopyableDetails("Favorites could not be loaded and cannot be changed in this session."); return; }
        if (favorites.Any(x => string.Equals(x.Path, path, StringComparison.OrdinalIgnoreCase))) { Status.Text = "This folder is already a favorite."; return; }
        try
        {
            favoritesStore.Save(favorites.Select(x => x.Path).Append(path));
            favorites.Add(FavoriteLocation(path));
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or System.Security.SecurityException)
        { ShowCopyableDetails($"Could not save favorites: {ex.Message}"); }
    }

    private void RemoveFavorite_Click(object sender, RoutedEventArgs e)
    {
        if (!favoritesAvailable) { ShowCopyableDetails("Favorites could not be loaded and cannot be changed in this session."); return; }
        if (sender is not Button { Tag: string path }) return;
        try
        {
            favoritesStore.Save(favorites.Where(x => !string.Equals(x.Path, path, StringComparison.OrdinalIgnoreCase)).Select(x => x.Path));
            var item = favorites.FirstOrDefault(x => string.Equals(x.Path, path, StringComparison.OrdinalIgnoreCase));
            if (item is not null) favorites.Remove(item);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or System.Security.SecurityException)
        { ShowCopyableDetails($"Could not save favorites: {ex.Message}"); }
    }

    private void NewTab_Click(object sender, RoutedEventArgs e) => AddTab();

    private void AddTab()
    {
        var tab = new TabState();
        tabs.Add(tab);
        Tabs.SelectedItem = tab;
    }

    private void Tab_PreviewMouseDown(object sender, MouseButtonEventArgs e)
    {
        if (e.ChangedButton != MouseButton.Middle || sender is not ListBoxItem { DataContext: TabState tab }) return;
        CloseTab(tab);
        e.Handled = true;
    }

    private void CloseTab(TabState tab)
    {
        if (tabs.Count == 1) return;
        int index = tabs.IndexOf(tab);
        if (ReferenceEquals(Tabs.SelectedItem, tab)) Tabs.SelectedItem = tabs[index > 0 ? index - 1 : 1];
        tabs.Remove(tab);
    }

    private async void Tabs_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (Tabs.SelectedItem is not TabState tab || ReferenceEquals(activeTab, tab)) return;
        loading?.Cancel();
        sizeLoading?.Cancel();
        activeTab = tab;
        FileList.ItemsSource = null;
        Breadcrumbs.ItemsSource = null;
        Address.Text = "";
        BackButton.IsEnabled = false;
        ForwardButton.IsEnabled = false;
        await NavigateAsync(tab.Path ?? Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), tab.HistoryIndex >= 0 ? tab.HistoryIndex : null);
    }

    private void Window_PreviewKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.F5 && Keyboard.Modifiers == ModifierKeys.None) { _ = RefreshCurrentAsync(); e.Handled = true; return; }
        if (FileList.IsKeyboardFocusWithin)
        {
            if (e.Key == Key.F2 && Keyboard.Modifiers == ModifierKeys.None) { _ = RenameSelectedAsync(); e.Handled = true; return; }
            if (e.Key == Key.Delete && Keyboard.Modifiers == ModifierKeys.None) { _ = DeleteSelectedAsync(); e.Handled = true; return; }
            if (Keyboard.Modifiers == ModifierKeys.Control)
            {
                if (e.Key == Key.C) { CopySelection(false); e.Handled = true; return; }
                if (e.Key == Key.X) { CopySelection(true); e.Handled = true; return; }
                if (e.Key == Key.V) { _ = PasteAsync(); e.Handled = true; return; }
            }
        }
        if ((Keyboard.Modifiers & ModifierKeys.Control) == 0) return;
        if (e.Key == Key.T) { AddTab(); e.Handled = true; }
        else if (e.Key == Key.W) { CloseTab(activeTab); e.Handled = true; }
        else if (e.Key == Key.Tab && tabs.Count > 1)
        {
            int direction = (Keyboard.Modifiers & ModifierKeys.Shift) != 0 ? -1 : 1;
            Tabs.SelectedIndex = (Tabs.SelectedIndex + direction + tabs.Count) % tabs.Count;
            e.Handled = true;
        }
    }

    private async void SidebarItem_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button { Tag: string path }) await NavigateAsync(path);
    }

    private void Search_Click(object sender, RoutedEventArgs e)
    {
        if (currentPath is null or DrivesPath) { Status.Text = "Open a folder before searching."; return; }
        var tab = activeTab;
        var window = new SearchWindow(currentPath, path => ReferenceEquals(tab, activeTab) ? NavigateAsync(path) : Task.CompletedTask) { Owner = this };
        window.ShowDialog();
    }

    private async Task LoadPlacesAsync()
    {
        try { PlacesList.ItemsSource = await Task.Run(SidebarLocations.GetPlaces); }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or System.Security.SecurityException or COMException)
        { Status.Text = $"Could not list common locations: {ex.Message}"; }
    }

    private async void RefreshDrives_Click(object sender, RoutedEventArgs e) => await RefreshDrivesAsync();

    private async void ShowDrives_Click(object sender, RoutedEventArgs e)
    {
        await NavigateAsync(DrivesPath);
    }

    private async Task RefreshDrivesAsync()
    {
        int version = ++driveRefreshVersion;
        DrivesStatus.Text = "Loading drives …";
        try
        {
            var drives = await Task.Run(SidebarLocations.GetDrives);
            if (version != driveRefreshVersion) return;
            DrivesList.ItemsSource = drives;
            DrivesStatus.Text = drives.Count == 0 ? "No drives found." : "";
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or System.Security.SecurityException)
        {
            if (version == driveRefreshVersion) DrivesStatus.Text = $"Could not list drives: {ex.Message}";
        }
    }

    private async Task NavigateAsync(string path, int? targetIndex = null, bool preserveSelection = false)
    {
        var selectedPaths = preserveSelection ? FileList.SelectedItems.OfType<FolderItem>().Select(item => item.Path).ToHashSet(StringComparer.OrdinalIgnoreCase) : null;
        loading?.Cancel();
        sizeLoading?.Cancel();
        if (FileList.ItemsSource is List<FolderItem> previousItems)
            foreach (var item in previousItems.Where(item => item.IsDirectory && item.Size == "Calculating …")) item.Size = "";
        var request = new CancellationTokenSource();
        loading = request;
        Status.Text = "Loading folder …";
        try
        {
            var result = await Task.Run(() =>
            {
                if (!string.Equals(path, DrivesPath, StringComparison.OrdinalIgnoreCase)) return FolderReader.Read(path, request.Token);
                request.Token.ThrowIfCancellationRequested();
                var items = SidebarLocations.GetDrives().Select(drive => new FolderItem(drive.Name, drive.Path, true, "Drive", null, DateTime.MinValue)).ToList();
                request.Token.ThrowIfCancellationRequested();
                return new FolderContents(DrivesPath, items);
            }, request.Token);
            if (request.IsCancellationRequested) return;
            currentPath = result.Path;
            activeTab.Title = currentPath == DrivesPath ? "Drives" : (Path.GetFileName(Path.TrimEndingDirectorySeparator(currentPath)) is { Length: > 0 } name ? name : currentPath);
            Address.Text = currentPath;
            Breadcrumbs.ItemsSource = currentPath == DrivesPath ? Array.Empty<Breadcrumb>() : BreadcrumbBuilder.Build(currentPath);
            FileList.ItemsSource = result.Items;
            ApplySort();
            if (selectedPaths is not null)
                foreach (var item in result.Items.Where(item => selectedPaths.Contains(item.Path))) FileList.SelectedItems.Add(item);
            if (currentPath != DrivesPath) StartSizeCalculation(result.Items);
            if (targetIndex is int index) historyIndex = index;
            else if (historyIndex < 0 || !string.Equals(history[historyIndex], currentPath, StringComparison.OrdinalIgnoreCase))
            {
                history.RemoveRange(historyIndex + 1, history.Count - historyIndex - 1);
                history.Add(currentPath);
                historyIndex = history.Count - 1;
            }
            BackButton.IsEnabled = historyIndex > 0;
            ForwardButton.IsEnabled = historyIndex < history.Count - 1;
            Status.Text = $"{result.Items.Count} items";
            StartWatching(currentPath);
        }
        catch (OperationCanceledException) { }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or ArgumentException or NotSupportedException or System.Security.SecurityException)
        {
            if (!request.IsCancellationRequested) Status.Text = $"Could not open folder: {ex.Message}";
        }
        finally
        {
            if (ReferenceEquals(loading, request)) loading = null;
            request.Dispose();
        }
    }

    private async void Refresh_Click(object sender, RoutedEventArgs e) => await RefreshCurrentAsync();

    private async Task RefreshCurrentAsync()
    {
        if (currentPath is not null) await NavigateAsync(currentPath, historyIndex, preserveSelection: true);
    }

    private async void StartWatching(string path)
    {
        if (folderWatcher is not null && string.Equals(folderWatcher.Path, path, StringComparison.OrdinalIgnoreCase)) return;
        refreshTimer.Stop();
        int version = ++watcherVersion;
        folderWatcher?.Dispose();
        folderWatcher = null;
        if (path == DrivesPath) return;
        FileSystemWatcher? watcher = null;
        try
        {
            watcher = await Task.Run(() => new FileSystemWatcher(path)
            {
                IncludeSubdirectories = false,
                NotifyFilter = NotifyFilters.FileName | NotifyFilters.DirectoryName | NotifyFilters.LastWrite | NotifyFilters.Size
            });
            if (windowClosed || version != watcherVersion || !string.Equals(currentPath, path, StringComparison.OrdinalIgnoreCase)) { watcher.Dispose(); return; }
            watcher.Changed += Watcher_Changed;
            watcher.Created += Watcher_Changed;
            watcher.Deleted += Watcher_Changed;
            watcher.Renamed += Watcher_Changed;
            watcher.Error += Watcher_Error;
            folderWatcher = watcher;
            await Task.Run(() => watcher.EnableRaisingEvents = true);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or ArgumentException or System.Security.SecurityException or ObjectDisposedException or InvalidOperationException)
        {
            if (ReferenceEquals(folderWatcher, watcher)) folderWatcher = null;
            watcher?.Dispose();
            if (!windowClosed && version == watcherVersion) Status.Text = $"Automatic refresh unavailable. Use F5 to refresh. {ex.Message}";
        }
    }

    private void Watcher_Changed(object sender, FileSystemEventArgs e)
    {
        _ = Dispatcher.BeginInvoke(() =>
        {
            if (!windowClosed && ReferenceEquals(sender, folderWatcher)) { refreshTimer.Stop(); refreshTimer.Start(); }
        });
    }

    private void Watcher_Error(object sender, ErrorEventArgs e)
    {
        _ = Dispatcher.BeginInvoke(() =>
        {
            if (windowClosed || !ReferenceEquals(sender, folderWatcher)) return;
            folderWatcher?.Dispose();
            folderWatcher = null;
            Status.Text = $"Automatic refresh unavailable. Use F5 to refresh. {e.GetException().Message}";
        });
    }

    private void FolderSizes_Click(object sender, RoutedEventArgs e)
    {
        if (currentPath != DrivesPath && FileList.ItemsSource is List<FolderItem> items) StartSizeCalculation(items);
    }

    private async void StartSizeCalculation(List<FolderItem> items)
    {
        sizeLoading?.Cancel();
        var request = new CancellationTokenSource();
        sizeLoading = request;
        var folders = items.Where(item => item.IsDirectory).ToList();
        foreach (var item in folders) { item.SizeBytes = null; item.Size = FolderSizes.IsChecked == true ? "Calculating …" : ""; item.SizeDetail = ""; }
        ScheduleSizeSort();
        try
        {
            if (FolderSizes.IsChecked != true) return;
            await Parallel.ForEachAsync(folders, new ParallelOptions { MaxDegreeOfParallelism = 2, CancellationToken = request.Token }, async (item, token) =>
            {
                await sizeWorkers.WaitAsync(token);
                string display;
                string detail = "";
                long? sizeBytes = null;
                try
                {
                    var size = await Task.Run(() => FolderSizeReader.Read(item.Path, token), token);
                    sizeBytes = size.Bytes;
                    display = $"{size.Bytes:N0} B";
                    if (size.SkippedLinks > 0)
                    {
                        display += " *";
                        detail = $"Calculated size: {size.Bytes:N0} B. Skipped {size.SkippedLinks:N0} symbolic link(s) or junction(s); their targets are not included. This is the logical size, not disk space used.";
                    }
                }
                catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or System.Security.SecurityException or OverflowException)
                { display = "Unavailable"; detail = $"Could not calculate the complete size: {ex.Message}"; }
                finally { sizeWorkers.Release(); }
                if (!token.IsCancellationRequested)
                    await Dispatcher.InvokeAsync(() => { if (!token.IsCancellationRequested) { item.SizeBytes = sizeBytes; item.Size = display; item.SizeDetail = detail; ScheduleSizeSort(); } });
            });
        }
        catch (OperationCanceledException) { }
        finally
        {
            if (ReferenceEquals(sizeLoading, request)) sizeLoading = null;
            request.Dispose();
        }
    }

    private void ColumnHeader_Click(object sender, RoutedEventArgs e)
    {
        if (e.OriginalSource is not GridViewColumnHeader { Tag: string column, Role: not GridViewColumnHeaderRole.Padding }) return;
        sortDescending = column == sortColumn && !sortDescending;
        sortColumn = column;
        if (FileList.View is GridView grid)
            foreach (var header in grid.Columns.Select(c => c.Header).OfType<GridViewColumnHeader>())
                header.Content = (string)header.Tag + ((string)header.Tag == column ? (sortDescending ? " ↓" : " ↑") : "");
        ApplySort();
    }

    private void ApplySort()
    {
        if (FileList.ItemsSource is not null && CollectionViewSource.GetDefaultView(FileList.ItemsSource) is ListCollectionView view)
            view.CustomSort = new FolderItemComparer(sortColumn, sortDescending);
    }

    private void ScheduleSizeSort()
    {
        if (sortColumn != "Size") return;
        if (!sortTimer.IsEnabled) sortTimer.Start();
    }

    private async void Go_Click(object sender, RoutedEventArgs e) => await NavigateAsync(Address.Text);
    private async void Address_KeyDown(object sender, KeyEventArgs e) { if (e.Key == Key.Enter) await NavigateAsync(Address.Text); }
    private async void Back_Click(object sender, RoutedEventArgs e) { if (historyIndex > 0) await NavigateAsync(history[historyIndex - 1], historyIndex - 1); }
    private async void Forward_Click(object sender, RoutedEventArgs e) { if (historyIndex + 1 < history.Count) await NavigateAsync(history[historyIndex + 1], historyIndex + 1); }
    private async void Up_Click(object sender, RoutedEventArgs e)
    {
        if (currentPath is null || currentPath == DrivesPath) return;
        if (Directory.GetParent(currentPath) is { } parent) await NavigateAsync(parent.FullName);
        else await NavigateAsync(DrivesPath);
    }
    private async void FileList_DoubleClick(object sender, MouseButtonEventArgs e) => await OpenSelectedAsync();
    private async void FileList_KeyDown(object sender, KeyEventArgs e) { if (e.Key == Key.Enter) await OpenSelectedAsync(); }

    private async void NewFolder_Click(object sender, RoutedEventArgs e) => await CreateItemAsync(true);
    private async void NewFile_Click(object sender, RoutedEventArgs e) => await CreateItemAsync(false);
    private async void Paste_Click(object sender, RoutedEventArgs e) => await PasteAsync();

    private void CopySelection(bool cut)
    {
        if (currentPath is null or DrivesPath || FileList.SelectedItems.Count == 0) { Status.Text = "Select files or folders first."; return; }
        try
        {
            var paths = new StringCollection();
            paths.AddRange(FileList.SelectedItems.OfType<FolderItem>().Select(item => item.Path).ToArray());
            var data = new DataObject();
            data.SetFileDropList(paths);
            data.SetData("Preferred DropEffect", new MemoryStream(BitConverter.GetBytes(cut ? 2 : 1)));
            Clipboard.SetDataObject(data, true);
            Status.Text = cut ? $"{paths.Count} item(s) ready to move." : $"{paths.Count} item(s) copied to clipboard.";
        }
        catch (ExternalException ex) { ShowCopyableDetails($"Could not access the clipboard: {ex.Message}"); }
    }

    private async Task PasteAsync()
    {
        if (fileOperationRunning) { Status.Text = "Another file operation is running."; return; }
        if (currentPath is null or DrivesPath) { Status.Text = "Open a destination folder first."; return; }
        try
        {
            var data = Clipboard.GetDataObject();
            if (data?.GetData(DataFormats.FileDrop) is not string[] paths || paths.Length == 0) { Status.Text = "The clipboard contains no files or folders."; return; }
            bool move = false;
            if (data.GetData("Preferred DropEffect") is MemoryStream effect && effect.Length >= 4)
            {
                byte[] bytes = effect.ToArray();
                move = (BitConverter.ToInt32(bytes, 0) & 2) != 0;
            }
            var tab = activeTab;
            string destination = currentPath;
            uint sequence = GetClipboardSequenceNumber();
            bool completed = await RunFileOperationAsync(paths, path => FileOperations.Transfer(path, destination, move), move ? "Moving" : "Copying");
            if (completed && move && paths.All(path => !File.Exists(path) && !Directory.Exists(path)) && GetClipboardSequenceNumber() == sequence)
                try { Clipboard.Clear(); } catch (ExternalException) { }
            if (ReferenceEquals(tab, activeTab) && currentPath == destination) await NavigateAsync(destination, historyIndex);
        }
        catch (ExternalException ex) { ShowCopyableDetails($"Could not access the clipboard: {ex.Message}"); }
    }

    private async Task DeleteSelectedAsync()
    {
        if (fileOperationRunning) { Status.Text = "Another file operation is running."; return; }
        if (currentPath is null or DrivesPath || FileList.SelectedItems.Count == 0) { Status.Text = "Select files or folders first."; return; }
        var paths = FileList.SelectedItems.OfType<FolderItem>().Select(item => item.Path).ToArray();
        if (MessageBox.Show(this, $"Move {paths.Length} selected item(s) to the Recycle Bin?", "Delete", MessageBoxButton.YesNo, MessageBoxImage.Question) != MessageBoxResult.Yes) return;
        var tab = activeTab;
        string parent = currentPath;
        await RunFileOperationAsync(paths, FileOperations.Recycle, "Deleting");
        if (ReferenceEquals(tab, activeTab) && currentPath == parent) await NavigateAsync(parent, historyIndex);
    }

    private async Task<bool> RunFileOperationAsync(string[] paths, Action<string> action, string label)
    {
        fileOperationRunning = true;
        int completed = 0;
        try
        {
            foreach (string path in paths)
            {
                Status.Text = $"{label} {completed + 1} of {paths.Length}: {Path.GetFileName(path)}";
                await RunOnStaAsync(() => action(path));
                completed++;
            }
            Status.Text = $"{label}: {completed} item(s) processed.";
            return true;
        }
        catch (OperationCanceledException) { ShowCopyableDetails($"{label} canceled. {completed} of {paths.Length} item(s) were processed; the current item may be partially processed."); }
        catch (Exception ex)
        { ShowCopyableDetails($"{label} stopped after {completed} of {paths.Length} item(s). The current item may be partially processed.\n\n{ex.Message}"); }
        finally { fileOperationRunning = false; }
        return false;
    }

    private static Task RunOnStaAsync(Action action)
    {
        var completion = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var thread = new Thread(() => { try { action(); completion.SetResult(); } catch (Exception ex) { completion.SetException(ex); } });
        thread.SetApartmentState(ApartmentState.STA);
        thread.IsBackground = true;
        thread.Start();
        return completion.Task;
    }

    private async Task CreateItemAsync(bool directory)
    {
        if (currentPath is null or DrivesPath) { Status.Text = "Open a folder first."; return; }
        string? name = PromptName(directory ? "New folder" : "New file", "");
        if (name is null) return;
        string parent = currentPath;
        try
        {
            ValidateName(name);
            string destination = Path.Combine(parent, name);
            await Task.Run(() =>
            {
                if (directory)
                {
                    if (!CreateDirectoryNative(destination, IntPtr.Zero)) throw new System.ComponentModel.Win32Exception(Marshal.GetLastWin32Error());
                }
                else using (new FileStream(destination, FileMode.CreateNew, FileAccess.Write)) { }
            });
            if (currentPath == parent) await NavigateAsync(parent, historyIndex);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or ArgumentException or NotSupportedException or System.Security.SecurityException or System.ComponentModel.Win32Exception)
        { ShowCopyableDetails($"Could not create item: {ex.Message}"); }
    }

    private async Task RenameSelectedAsync()
    {
        if (currentPath is null or DrivesPath || FileList.SelectedItems.Count != 1 || FileList.SelectedItem is not FolderItem item)
        { Status.Text = "Select one file or folder to rename."; return; }
        string? name = PromptName("Rename", item.Name);
        if (name is null || name == item.Name) return;
        string parent = currentPath;
        try
        {
            ValidateName(name);
            string destination = Path.Combine(parent, name);
            await Task.Run(() =>
            {
                if (item.IsDirectory) Directory.Move(item.Path, destination);
                else File.Move(item.Path, destination);
            });
            if (currentPath == parent) await NavigateAsync(parent, historyIndex);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or ArgumentException or NotSupportedException or System.Security.SecurityException)
        { ShowCopyableDetails($"Could not rename item: {ex.Message}"); }
    }

    private static void ValidateName(string name)
    {
        if (string.IsNullOrWhiteSpace(name) || name is "." or ".." || name.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0 || name.EndsWith(' ') || name.EndsWith('.'))
            throw new ArgumentException("Enter a valid file or folder name.");
    }

    private string? PromptName(string title, string initialValue)
    {
        var dialog = new Window { Title = title, Owner = this, WindowStartupLocation = WindowStartupLocation.CenterOwner, Width = 420, Height = 150, ResizeMode = ResizeMode.NoResize, ShowInTaskbar = false };
        var panel = new DockPanel { Margin = new Thickness(12) };
        var buttons = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right, Margin = new Thickness(0, 10, 0, 0) };
        DockPanel.SetDock(buttons, Dock.Bottom);
        var accept = new Button { Content = "OK", IsDefault = true, MinWidth = 75, Margin = new Thickness(0, 0, 8, 0) };
        var cancel = new Button { Content = "Cancel", IsCancel = true, MinWidth = 75 };
        accept.Click += (_, _) => dialog.DialogResult = true;
        buttons.Children.Add(accept); buttons.Children.Add(cancel);
        var input = new TextBox { Text = initialValue, VerticalContentAlignment = VerticalAlignment.Center };
        dialog.Loaded += (_, _) => { input.Focus(); input.SelectAll(); };
        panel.Children.Add(buttons); panel.Children.Add(input); dialog.Content = panel;
        return dialog.ShowDialog() == true ? input.Text : null;
    }

    private async Task OpenSelectedAsync()
    {
        if (FileList.SelectedItem is not FolderItem item) return;
        await OpenItemAsync(item);
    }

    private async Task OpenItemAsync(FolderItem item)
    {
        if (item.IsDirectory) { await NavigateAsync(item.Path); return; }
        try { Process.Start(new ProcessStartInfo(item.Path) { UseShellExecute = true }); }
        catch (Exception ex) when (ex is System.ComponentModel.Win32Exception or InvalidOperationException or IOException)
        { Status.Text = $"Could not open file: {ex.Message}"; }
    }

    private void Item_RightButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (sender is ListViewItem { IsSelected: false } item)
        {
            FileList.UnselectAll();
            item.IsSelected = true;
        }
    }

    private void Item_ContextMenuOpening(object sender, ContextMenuEventArgs e)
    {
        if (sender is not ListViewItem { DataContext: FolderItem item, ContextMenu: { } menu }) return;
        menu.Items.Clear();
        var open = new MenuItem { Header = "Open" };
        open.Click += async (_, _) => await OpenItemAsync(item);
        var copyPath = new MenuItem { Header = "Copy path" };
        copyPath.Click += (_, _) =>
        {
            try { Clipboard.SetText(item.Path); Status.Text = "Path copied to clipboard."; }
            catch (ExternalException) { Status.Text = "Could not access the clipboard."; }
        };
        var details = new MenuItem { Header = "Folder size details", IsEnabled = item.IsDirectory };
        details.Click += (_, _) =>
        {
            var message = !string.IsNullOrEmpty(item.SizeDetail) ? item.SizeDetail
                : item.Size == "Calculating …" ? "The folder size is still being calculated."
                : string.IsNullOrEmpty(item.Size) ? "Folder size calculation is disabled or was interrupted."
                : $"Calculated folder size: {item.Size}";
            ShowCopyableDetails(message);
        };
        menu.Items.Add(open);
        var copy = new MenuItem { Header = "Copy", InputGestureText = "Ctrl+C", IsEnabled = currentPath != DrivesPath };
        copy.Click += (_, _) => CopySelection(false);
        var cut = new MenuItem { Header = "Cut", InputGestureText = "Ctrl+X", IsEnabled = currentPath != DrivesPath };
        cut.Click += (_, _) => CopySelection(true);
        var paste = new MenuItem { Header = "Paste", InputGestureText = "Ctrl+V", IsEnabled = currentPath != DrivesPath };
        paste.Click += async (_, _) => await PasteAsync();
        var rename = new MenuItem { Header = "Rename", InputGestureText = "F2", IsEnabled = currentPath != DrivesPath && FileList.SelectedItems.Count == 1 };
        rename.Click += async (_, _) => await RenameSelectedAsync();
        var delete = new MenuItem { Header = "Delete", InputGestureText = "Del", IsEnabled = currentPath != DrivesPath };
        delete.Click += async (_, _) => await DeleteSelectedAsync();
        var favorite = new MenuItem { Header = "Add to favorites", IsEnabled = item.IsDirectory && FileList.SelectedItems.Count == 1 };
        favorite.Click += (_, _) => AddFavorite(item.Path);
        menu.Items.Add(copy);
        menu.Items.Add(cut);
        menu.Items.Add(paste);
        menu.Items.Add(rename);
        menu.Items.Add(delete);
        menu.Items.Add(favorite);
        menu.Items.Add(copyPath);
        menu.Items.Add(new Separator());
        menu.Items.Add(details);
    }

    private void ShowCopyableDetails(string message)
    {
        var dialog = new Window
        {
            Title = "Details", Owner = this, WindowStartupLocation = WindowStartupLocation.CenterOwner,
            Width = 560, Height = 260, MinWidth = 360, MinHeight = 180, ShowInTaskbar = false
        };
        var panel = new DockPanel { Margin = new Thickness(12) };
        var buttons = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right, Margin = new Thickness(0, 12, 0, 0) };
        DockPanel.SetDock(buttons, Dock.Bottom);
        var copy = new Button { Content = "Copy", MinWidth = 80, Padding = new Thickness(12, 4, 12, 4), Margin = new Thickness(0, 0, 8, 0) };
        var close = new Button { Content = "Close", MinWidth = 80, Padding = new Thickness(12, 4, 12, 4), IsCancel = true };
        var text = new TextBox { Text = message, IsReadOnly = true, TextWrapping = TextWrapping.Wrap, VerticalScrollBarVisibility = ScrollBarVisibility.Auto };
        copy.Click += (_, _) =>
        {
            try { Clipboard.SetText(message); copy.Content = "Copied"; }
            catch (ExternalException) { copy.Content = "Copy failed"; }
        };
        buttons.Children.Add(copy);
        buttons.Children.Add(close);
        panel.Children.Add(buttons);
        panel.Children.Add(text);
        dialog.Content = panel;
        dialog.ShowDialog();
    }
}
