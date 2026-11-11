using System.IO;
using System.Runtime.InteropServices;

namespace Xplorer;

internal sealed record SidebarLocation(string Name, string Path);

internal static class SidebarLocations
{
    private static readonly Guid DownloadsId = new("374DE290-123F-4565-9164-39C4925E467B");

    [DllImport("shell32.dll", ExactSpelling = true)]
    private static extern int SHGetKnownFolderPath(in Guid folderId, uint flags, IntPtr token, out IntPtr path);

    public static List<SidebarLocation> GetPlaces()
    {
        var locations = new List<SidebarLocation>();
        Add("Home", Environment.GetFolderPath(Environment.SpecialFolder.UserProfile));
        Add("Desktop", Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory));
        Add("Documents", Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments));
        Add("Downloads", GetDownloadsPath());
        return locations;

        void Add(string name, string path)
        {
            if (!string.IsNullOrWhiteSpace(path)) locations.Add(new(name, path));
        }
    }

    public static List<SidebarLocation> GetDrives() =>
        DriveInfo.GetDrives().Select(drive => new SidebarLocation(drive.Name, drive.Name)).ToList();

    private static string GetDownloadsPath()
    {
        int result = SHGetKnownFolderPath(DownloadsId, 0x00004000, IntPtr.Zero, out var path);
        try { return result >= 0 && path != IntPtr.Zero ? Marshal.PtrToStringUni(path) ?? "" : ""; }
        finally { if (path != IntPtr.Zero) Marshal.FreeCoTaskMem(path); }
    }
}
