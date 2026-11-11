using System.Text.Json;
using System.IO;

namespace Xplorer;

internal sealed class FavoritesStore(string path)
{
    public List<string> Load()
    {
        if (!File.Exists(path)) return [];
        return (JsonSerializer.Deserialize<List<string>>(File.ReadAllText(path)) ?? [])
            .Where(p => !string.IsNullOrWhiteSpace(p) && Path.IsPathFullyQualified(p))
            .Distinct(StringComparer.OrdinalIgnoreCase).ToList();
    }

    public void Save(IEnumerable<string> favorites)
    {
        var directory = Path.GetDirectoryName(path) ?? throw new IOException("Invalid favorites path.");
        Directory.CreateDirectory(directory);
        var temporary = Path.Combine(directory, $".favorites-{Guid.NewGuid():N}.tmp");
        try
        {
            File.WriteAllText(temporary, JsonSerializer.Serialize(favorites));
            File.Move(temporary, path, true);
        }
        finally { if (File.Exists(temporary)) File.Delete(temporary); }
    }
}
