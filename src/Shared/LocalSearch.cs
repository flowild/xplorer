using System.IO;

namespace Xplorer;

internal sealed record SearchResult(string Name, string Path, bool IsDirectory);
internal sealed record SearchResults(List<SearchResult> Items, int SkippedFolders, bool LimitReached);

internal static class LocalSearch
{
    public static SearchResults Find(string root, string query, CancellationToken token, int limit = 1000)
    {
        var results = new List<SearchResult>();
        var pending = new Stack<string>();
        pending.Push(root);
        int skipped = 0;
        while (pending.TryPop(out var folder))
        {
            token.ThrowIfCancellationRequested();
            try
            {
                foreach (var entry in new DirectoryInfo(folder).EnumerateFileSystemInfos())
                {
                    token.ThrowIfCancellationRequested();
                    var attributes = entry.Attributes;
                    bool directory = (attributes & FileAttributes.Directory) != 0;
                    if (entry.Name.Contains(query, StringComparison.CurrentCultureIgnoreCase))
                    {
                        results.Add(new(entry.Name, entry.FullName, directory));
                        if (results.Count >= limit) return new(results, skipped, true);
                    }
                    if (directory && (attributes & FileAttributes.ReparsePoint) == 0) pending.Push(entry.FullName);
                }
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or System.Security.SecurityException) { skipped++; }
        }
        return new(results, skipped, false);
    }
}
