using System.IO;
using System.ComponentModel;

namespace Xplorer;

internal sealed class FolderItem(string name, string path, bool isDirectory, string type, long? sizeBytes, DateTime modified) : INotifyPropertyChanged
{
    public string Name { get; } = name;
    public string Path { get; } = path;
    public bool IsDirectory { get; } = isDirectory;
    public string Type { get; } = type;
    public string Modified { get; } = modified == DateTime.MinValue ? "" : modified.ToString("g");
    public DateTime ModifiedValue { get; } = modified;
    public long? SizeBytes { get; set; } = sizeBytes;
    private string sizeDetail = "";
    public string SizeDetail
    {
        get => sizeDetail;
        set { if (sizeDetail != value) { sizeDetail = value; PropertyChanged?.Invoke(this, new(nameof(SizeDetail))); } }
    }
    private string size = sizeBytes is long bytes ? $"{bytes:N0} B" : "";
    public string Size
    {
        get => size;
        set { if (size != value) { size = value; PropertyChanged?.Invoke(this, new(nameof(Size))); } }
    }
    public event PropertyChangedEventHandler? PropertyChanged;
}
internal sealed record FolderContents(string Path, List<FolderItem> Items);

internal static class FolderReader
{
    public static FolderContents Read(string path, CancellationToken cancellationToken, bool showHiddenItems = true)
    {
        var directory = new DirectoryInfo(System.IO.Path.GetFullPath(path));
        var items = new List<FolderItem>();
        foreach (var entry in directory.EnumerateFileSystemInfos())
        {
            cancellationToken.ThrowIfCancellationRequested();
            try
            {
                var attributes = entry.Attributes;
                if (!showHiddenItems && (attributes & FileAttributes.Hidden) != 0) continue;
                bool isDirectory = (attributes & FileAttributes.Directory) != 0;
                long? size = !isDirectory && entry is FileInfo file ? file.Length : null;
                items.Add(new(entry.Name, entry.FullName, isDirectory, isDirectory ? "Folder" : entry.Extension, size, entry.LastWriteTime));
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException) { /* A vanished entry must not block the folder. */ }
        }
        cancellationToken.ThrowIfCancellationRequested();
        return new(directory.FullName, items.OrderByDescending(x => x.IsDirectory).ThenBy(x => x.Name, StringComparer.CurrentCultureIgnoreCase).ToList());
    }
}

internal sealed record FolderSizeResult(long Bytes, int SkippedLinks);

internal static class FolderSizeReader
{
    public static FolderSizeResult Read(string path, CancellationToken cancellationToken)
    {
        long total = 0;
        int skippedLinks = 0;
        var pending = new Stack<string>();
        pending.Push(path);
        while (pending.TryPop(out var directory))
        {
            cancellationToken.ThrowIfCancellationRequested();
            var current = new DirectoryInfo(directory);
            if ((current.Attributes & FileAttributes.ReparsePoint) != 0)
            {
                if (current.LinkTarget is null) throw new IOException($"Unsupported redirected folder: {directory}");
                skippedLinks = checked(skippedLinks + 1);
                continue;
            }
            foreach (var entry in new DirectoryInfo(directory).EnumerateFileSystemInfos())
            {
                cancellationToken.ThrowIfCancellationRequested();
                var attributes = entry.Attributes;
                if ((attributes & FileAttributes.ReparsePoint) != 0)
                {
                    if (entry.LinkTarget is null) throw new IOException($"Unsupported redirected entry: {entry.FullName}");
                    skippedLinks = checked(skippedLinks + 1);
                    continue;
                }
                if ((attributes & FileAttributes.Directory) != 0) pending.Push(entry.FullName);
                else if (entry is FileInfo file) total = checked(total + file.Length);
            }
        }
        return new(total, skippedLinks);
    }
}
