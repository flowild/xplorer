using System.Collections;
using System.IO;

namespace Xplorer;

internal sealed record Breadcrumb(string Name, string Path);

internal static class BreadcrumbBuilder
{
    public static List<Breadcrumb> Build(string path)
    {
        string root = Path.GetPathRoot(path) ?? "";
        if (root.Length == 0) return [];
        var result = new List<Breadcrumb> { new(root, root) };
        string current = root;
        foreach (var part in path[root.Length..].Split([Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar], StringSplitOptions.RemoveEmptyEntries))
        {
            current = Path.Combine(current, part);
            result.Add(new(part, current));
        }
        return result;
    }
}

internal sealed class FolderItemComparer(string column, bool descending) : IComparer
{
    public int Compare(object? x, object? y)
    {
        if (x is not FolderItem a || y is not FolderItem b) return 0;
        int folders = b.IsDirectory.CompareTo(a.IsDirectory);
        if (folders != 0) return folders;
        if (column == "Size" && a.SizeBytes.HasValue != b.SizeBytes.HasValue)
            return a.SizeBytes.HasValue ? -1 : 1;
        int order = column switch
        {
            "Type" => StringComparer.CurrentCultureIgnoreCase.Compare(a.Type, b.Type),
            "Size" => Nullable.Compare(a.SizeBytes, b.SizeBytes),
            "Modified" => DateTime.Compare(a.ModifiedValue, b.ModifiedValue),
            _ => StringComparer.CurrentCultureIgnoreCase.Compare(a.Name, b.Name)
        };
        if (descending) order = -Math.Sign(order);
        return order != 0 ? order : StringComparer.CurrentCultureIgnoreCase.Compare(a.Name, b.Name);
    }
}
