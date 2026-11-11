using System.IO;
using Microsoft.VisualBasic.FileIO;

namespace Xplorer;

internal static class FileOperations
{
    public static void Transfer(string source, string destinationFolder, bool move, bool showDialogs = true)
    {
        source = Path.GetFullPath(source);
        destinationFolder = Path.GetFullPath(destinationFolder);
        string name = Path.GetFileName(Path.TrimEndingDirectorySeparator(source));
        if (string.IsNullOrEmpty(name)) throw new IOException("A drive root cannot be transferred.");
        var attributes = File.GetAttributes(source);
        bool directory = (attributes & FileAttributes.Directory) != 0;
        string destination = Path.Combine(destinationFolder, name);
        if (string.Equals(source.TrimEnd(Path.DirectorySeparatorChar), destination.TrimEnd(Path.DirectorySeparatorChar), StringComparison.OrdinalIgnoreCase))
        {
            if (move) throw new IOException("The item is already in this folder.");
            destination = GetCopyPath(destinationFolder, name, directory);
        }
        if (directory && (string.Equals(Path.TrimEndingDirectorySeparator(destinationFolder), Path.TrimEndingDirectorySeparator(source), StringComparison.OrdinalIgnoreCase) || destinationFolder.StartsWith(Path.TrimEndingDirectorySeparator(source) + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase)))
            throw new IOException("A folder cannot be transferred into itself.");
        if (showDialogs)
        {
            if (directory && move) FileSystem.MoveDirectory(source, destination, UIOption.AllDialogs, UICancelOption.ThrowException);
            else if (directory) FileSystem.CopyDirectory(source, destination, UIOption.AllDialogs, UICancelOption.ThrowException);
            else if (move) FileSystem.MoveFile(source, destination, UIOption.AllDialogs, UICancelOption.ThrowException);
            else FileSystem.CopyFile(source, destination, UIOption.AllDialogs, UICancelOption.ThrowException);
        }
        else
        {
            if (directory && move) FileSystem.MoveDirectory(source, destination, false);
            else if (directory) FileSystem.CopyDirectory(source, destination, false);
            else if (move) FileSystem.MoveFile(source, destination, false);
            else FileSystem.CopyFile(source, destination, false);
        }
    }

    private static string GetCopyPath(string folder, string name, bool directory)
    {
        string extension = directory ? "" : Path.GetExtension(name);
        string stem = directory ? name : name[..^extension.Length];
        for (int number = 1; number < int.MaxValue; number++)
        {
            string suffix = number == 1 ? " - Copy" : $" - Copy ({number})";
            string candidate = Path.Combine(folder, stem + suffix + extension);
            if (!File.Exists(candidate) && !Directory.Exists(candidate)) return candidate;
        }
        throw new IOException("Could not find an available name for the copy.");
    }

    public static void Recycle(string source)
    {
        source = Path.GetFullPath(source);
        string root = Path.GetPathRoot(source) ?? throw new IOException("Invalid path.");
        if (string.Equals(Path.TrimEndingDirectorySeparator(source), Path.TrimEndingDirectorySeparator(root), StringComparison.OrdinalIgnoreCase))
            throw new IOException("A drive root cannot be deleted.");
        if (root.StartsWith(@"\\") || new DriveInfo(root).DriveType != DriveType.Fixed)
            throw new IOException("The Recycle Bin is not guaranteed for this drive. Deletion is unavailable here.");
        var attributes = File.GetAttributes(source);
        if ((attributes & FileAttributes.ReparsePoint) != 0)
            throw new IOException("Deleting linked or redirected entries is not supported yet.");
        if ((attributes & FileAttributes.Directory) != 0)
            FileSystem.DeleteDirectory(source, UIOption.AllDialogs, RecycleOption.SendToRecycleBin, UICancelOption.ThrowException);
        else FileSystem.DeleteFile(source, UIOption.AllDialogs, RecycleOption.SendToRecycleBin, UICancelOption.ThrowException);
    }
}
