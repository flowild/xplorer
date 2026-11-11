using System.IO;
using System.Runtime.InteropServices;
using Microsoft.UI.Input;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using Windows.ApplicationModel.DataTransfer;
using Windows.Storage;
using Windows.System;
using Windows.UI.Core;

namespace Xplorer.WinUI;

public sealed partial class MainWindow
{
    [DllImport("kernel32.dll", EntryPoint = "CreateDirectoryW", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern bool CreateDirectoryNative(string path, IntPtr attributes);

    private async void Root_KeyDown(object sender, KeyRoutedEventArgs e)
    {
        bool control = (InputKeyboardSource.GetKeyStateForCurrentThread(VirtualKey.Control) & CoreVirtualKeyStates.Down) != 0;
        bool shift = (InputKeyboardSource.GetKeyStateForCurrentThread(VirtualKey.Shift) & CoreVirtualKeyStates.Down) != 0;
        if (control && e.Key == VirtualKey.T) { AddTab(); e.Handled = true; }
        else if (control && e.Key == VirtualKey.W && Tabs.SelectedItem is TabViewItem tab && Tabs.TabItems.Count > 1) { Tabs.TabItems.Remove(tab); e.Handled = true; }
        else if (control && e.Key == VirtualKey.Tab && Tabs.TabItems.Count > 1) { Tabs.SelectedIndex = (Tabs.SelectedIndex + (shift ? -1 : 1) + Tabs.TabItems.Count) % Tabs.TabItems.Count; e.Handled = true; }
        else if (e.Key == VirtualKey.F5 && active?.Path is { } path) { e.Handled = true; await NavigateAsync(path, active.Index); }
        else if (IsFileListFocused())
        {
            if (control && e.Key == VirtualKey.C) { e.Handled = true; await CopySelectionAsync(false); }
            else if (control && e.Key == VirtualKey.X) { e.Handled = true; await CopySelectionAsync(true); }
            else if (control && e.Key == VirtualKey.V) { e.Handled = true; await PasteAsync(); }
            else if (e.Key == VirtualKey.Delete && !control && !shift) { e.Handled = true; await DeleteAsync(); }
            else if (e.Key == VirtualKey.F2 && !control && !shift) { e.Handled = true; await RenameAsync(); }
        }
    }

    private bool IsFileListFocused()
    {
        var element = FocusManager.GetFocusedElement(Files.XamlRoot) as DependencyObject;
        while (element is not null) { if (ReferenceEquals(element, Files)) return true; element = VisualTreeHelper.GetParent(element); }
        return false;
    }

    private void Files_RightTapped(object sender, RightTappedRoutedEventArgs e)
    {
        DependencyObject? element = e.OriginalSource as DependencyObject;
        while (element is not null && element != Files && element is not ListViewItem) element = VisualTreeHelper.GetParent(element);
        var menu = new MenuFlyout();
        if (element is ListViewItem { DataContext: FolderItem item })
        {
            if (!Files.SelectedItems.Contains(item)) { Files.SelectedItems.Clear(); Files.SelectedItems.Add(item); }
            Add("Open", async () => await OpenItemAsync(item));
            Add("Copy", async () => await CopySelectionAsync(false));
            Add("Cut", async () => await CopySelectionAsync(true));
            Add("Paste", PasteAsync);
            Add("Rename", RenameAsync);
            Add("Delete", DeleteAsync);
            if (item.IsDirectory) Add("Add to favorites", () => { if (!favorites.Contains(item.Path, StringComparer.OrdinalIgnoreCase)) SaveFavorites([.. favorites, item.Path]); return Task.CompletedTask; });
            Add("Copy path", () => { var data = new DataPackage(); data.SetText(item.Path); Clipboard.SetContent(data); return Task.CompletedTask; });
            if (item.IsDirectory) Add("Folder size details", () => ShowDetailsAsync(string.IsNullOrEmpty(item.SizeDetail) ? $"Folder size: {item.Size}" : item.SizeDetail));
        }
        else
        {
            Add("New folder", () => CreateAsync(true));
            Add("New file", () => CreateAsync(false));
            Add("Paste", PasteAsync);
        }
        menu.ShowAt(Files, e.GetPosition(Files));
        e.Handled = true;
        void Add(string label, Func<Task> action)
        {
            var menuItem = new MenuFlyoutItem { Text = label };
            menuItem.Click += async (_, _) => { try { await action(); } catch (Exception ex) { await ShowDetailsAsync(ex.Message); } };
            menu.Items.Add(menuItem);
        }
    }

    private async Task OpenItemAsync(FolderItem item)
    {
        if (item.IsDirectory) await NavigateAsync(item.Path);
        else try { System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(item.Path) { UseShellExecute = true }); }
        catch (Exception ex) when (ex is System.ComponentModel.Win32Exception or InvalidOperationException or IOException) { await ShowDetailsAsync($"Could not open file: {ex.Message}"); }
    }

    private async Task CopySelectionAsync(bool cut)
    {
        if (active?.Path is null or DrivesPath || Files.SelectedItems.Count == 0) { Status.Text = "Select files or folders first."; return; }
        try
        {
            var selected = Files.SelectedItems.OfType<FolderItem>().ToArray();
            var storage = new List<IStorageItem>();
            foreach (var item in selected) storage.Add(item.IsDirectory ? await StorageFolder.GetFolderFromPathAsync(item.Path) : await StorageFile.GetFileFromPathAsync(item.Path));
            var data = new DataPackage { RequestedOperation = cut ? DataPackageOperation.Move : DataPackageOperation.Copy };
            data.SetStorageItems(storage); Clipboard.SetContent(data); Clipboard.Flush();
            Status.Text = cut ? $"{storage.Count} item(s) ready to move." : $"{storage.Count} item(s) copied.";
        }
        catch (Exception ex) { await ShowDetailsAsync($"Could not copy selection: {ex.Message}"); }
    }

    private async Task PasteAsync()
    {
        if (fileOperationRunning || searching || active?.Path is null or DrivesPath) { Status.Text = "Open a destination folder first or wait for the current operation."; return; }
        try
        {
            var content = Clipboard.GetContent();
            if (!content.Contains(StandardDataFormats.StorageItems)) { Status.Text = "The clipboard contains no files or folders."; return; }
            var selected = await content.GetStorageItemsAsync();
            var paths = selected.Select(x => x.Path).Where(x => !string.IsNullOrEmpty(x)).ToArray();
            if (paths.Length == 0) { Status.Text = "The clipboard contains no accessible file paths."; return; }
            bool move = (content.RequestedOperation & DataPackageOperation.Move) != 0;
            var tab = active; string destination = tab.Path!;
            bool completed = await RunOperationAsync(paths, p => FileOperations.Transfer(p, destination, move), move ? "Moving" : "Copying");
            if (completed && move) content.ReportOperationCompleted(DataPackageOperation.Move);
            if (ReferenceEquals(tab, active) && active.Path == destination) await NavigateAsync(destination, active.Index);
        }
        catch (Exception ex) { await ShowDetailsAsync($"Could not paste: {ex.Message}"); }
    }
    private async Task DeleteAsync()
    {
        if (fileOperationRunning || searching || active?.Path is null or DrivesPath || Files.SelectedItems.Count == 0) { Status.Text = "Select files or folders in an open folder."; return; }
        var paths = Files.SelectedItems.OfType<FolderItem>().Select(x => x.Path).ToArray();
        var confirm = new ContentDialog { Title = "Delete", Content = $"Move {paths.Length} selected item(s) to the Recycle Bin?", PrimaryButtonText = "Delete", CloseButtonText = "Cancel", XamlRoot = Files.XamlRoot };
        if (await confirm.ShowAsync() != ContentDialogResult.Primary) return;
        var tab = active; string parent = tab.Path!;
        await RunOperationAsync(paths, FileOperations.Recycle, "Deleting");
        if (ReferenceEquals(tab, active) && active.Path == parent) await NavigateAsync(parent, active.Index);
    }
    private async Task<bool> RunOperationAsync(string[] paths, Action<string> action, string label)
    {
        fileOperationRunning = true; int completed = 0;
        try
        {
            foreach (string path in paths)
            {
                Status.Text = $"{label} {completed + 1} of {paths.Length}: {Path.GetFileName(path)}";
                var completion = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
                var thread = new Thread(() => { try { action(path); completion.SetResult(); } catch (Exception ex) { completion.SetException(ex); } });
                thread.SetApartmentState(ApartmentState.STA); thread.IsBackground = true; thread.Start();
                await completion.Task; completed++;
            }
            Status.Text = $"{label}: {completed} item(s) processed."; return true;
        }
        catch (Exception ex) { await ShowDetailsAsync($"{label} stopped after {completed} of {paths.Length} item(s). The current item may be partially processed.\n\n{ex.Message}"); return false; }
        finally { fileOperationRunning = false; }
    }

    private async Task CreateAsync(bool directory)
    {
        if (fileOperationRunning || searching || active?.Path is null or DrivesPath) { Status.Text = "Open a folder first or wait for the current operation."; return; }
        string? name = await PromptAsync(directory ? "New folder" : "New file", "");
        if (name is null) return;
        var tab = active; string parent = tab.Path!;
        try
        {
            ValidateName(name); string destination = Path.Combine(parent, name);
            await Task.Run(() => { if (directory) { if (!CreateDirectoryNative(destination, IntPtr.Zero)) throw new System.ComponentModel.Win32Exception(Marshal.GetLastWin32Error()); } else using (new FileStream(destination, FileMode.CreateNew, FileAccess.Write)) { } });
            if (ReferenceEquals(tab, active) && active.Path == parent) await NavigateAsync(parent, active.Index);
        }
        catch (Exception ex) { await ShowDetailsAsync($"Could not create item: {ex.Message}"); }
    }
    private async Task RenameAsync()
    {
        if (fileOperationRunning || searching || active?.Path is null or DrivesPath || Files.SelectedItems.Count != 1 || Files.SelectedItem is not FolderItem item) { Status.Text = "Select one file or folder to rename and wait for any operation to finish."; return; }
        string? name = await PromptAsync("Rename", item.Name);
        if (name is null || name == item.Name) return;
        var tab = active; string parent = tab.Path!;
        try
        {
            ValidateName(name); string destination = Path.Combine(parent, name);
            await Task.Run(() => { if (item.IsDirectory) Directory.Move(item.Path, destination); else File.Move(item.Path, destination); });
            if (ReferenceEquals(tab, active) && active.Path == parent) await NavigateAsync(parent, active.Index);
        }
        catch (Exception ex) { await ShowDetailsAsync($"Could not rename item: {ex.Message}"); }
    }
    private static void ValidateName(string name)
    {
        if (string.IsNullOrWhiteSpace(name) || name is "." or ".." || name.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0 || name.EndsWith(' ') || name.EndsWith('.')) throw new ArgumentException("Enter a valid file or folder name.");
    }
    private async Task<string?> PromptAsync(string title, string initial)
    {
        var input = new TextBox { Text = initial, MinWidth = 300 };
        var dialog = new ContentDialog { Title = title, Content = input, PrimaryButtonText = "OK", CloseButtonText = "Cancel", DefaultButton = ContentDialogButton.Primary, XamlRoot = Files.XamlRoot };
        return await dialog.ShowAsync() == ContentDialogResult.Primary ? input.Text : null;
    }
    private async Task ShowDetailsAsync(string message)
    {
        var text = new TextBox { Text = message, IsReadOnly = true, TextWrapping = TextWrapping.Wrap, AcceptsReturn = true, MinWidth = 320 };
        var dialog = new ContentDialog { Title = "Details", Content = text, CloseButtonText = "Close", XamlRoot = Files.XamlRoot };
        await dialog.ShowAsync();
    }
}
