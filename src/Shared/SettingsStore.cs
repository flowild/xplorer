using System.IO;
using System.Text.Json;

namespace Xplorer;

internal sealed record AppSettings(bool CalculateFolderSizes = true, double SidebarWidth = 200);

internal sealed class SettingsStore(string path)
{
    public AppSettings Load()
    {
        if (!File.Exists(path)) return new();
        var settings = JsonSerializer.Deserialize<AppSettings>(File.ReadAllText(path)) ?? throw new JsonException("Settings are empty.");
        return settings with { SidebarWidth = double.IsFinite(settings.SidebarWidth) ? Math.Clamp(settings.SidebarWidth, 140, 400) : 200 };
    }

    public void Save(AppSettings settings)
    {
        var directory = Path.GetDirectoryName(path) ?? throw new IOException("Invalid settings path.");
        Directory.CreateDirectory(directory);
        var temporary = Path.Combine(directory, $".settings-{Guid.NewGuid():N}.tmp");
        try { File.WriteAllText(temporary, JsonSerializer.Serialize(settings)); File.Move(temporary, path, true); }
        finally { if (File.Exists(temporary)) File.Delete(temporary); }
    }
}
