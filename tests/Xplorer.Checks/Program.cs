using System.Diagnostics;
using Xplorer;

int benchmarkCount = 2000;
int countIndex = Array.IndexOf(args, "--count");
if (countIndex >= 0 && (!args.Contains("--benchmark") || countIndex + 1 >= args.Length || !int.TryParse(args[countIndex + 1], out benchmarkCount) || benchmarkCount < 1 || benchmarkCount > 100000))
{
    Console.Error.WriteLine("Use --benchmark --count with an integer between 1 and 100000.");
    return 2;
}

string root = Path.Combine(Path.GetTempPath(), "Xplorer-checks-" + Guid.NewGuid().ToString("N"));
Directory.CreateDirectory(root);
int failures = 0;
try
{
    string source = Directory.CreateDirectory(Path.Combine(root, "source")).FullName;
    string destination = Directory.CreateDirectory(Path.Combine(root, "destination")).FullName;
    string nested = Directory.CreateDirectory(Path.Combine(source, "nested")).FullName;
    File.WriteAllText(Path.Combine(source, "alpha.txt"), "alpha");
    File.WriteAllText(Path.Combine(nested, "needle.txt"), "needle");

    Check("folder listing and metadata", () =>
    {
        var entries = FolderReader.Read(source, CancellationToken.None).Items;
        Assert(entries.Count == 2 && entries[0].Name == "nested" && entries[0].IsDirectory, "Expected folder followed by file.");
        Assert(entries.Single(x => x.Name == "alpha.txt").SizeBytes == 5, "File size differs.");
    });
    Check("hidden entries in folder listing and search", () =>
    {
        string folder = Directory.CreateDirectory(Path.Combine(root, "hidden-test")).FullName;
        string visible = Path.Combine(folder, "visible-match.txt");
        string hiddenFile = Path.Combine(folder, "hidden-match.txt");
        string hiddenFolder = Directory.CreateDirectory(Path.Combine(folder, "hidden-folder")).FullName;
        File.WriteAllText(visible, "visible"); File.WriteAllText(hiddenFile, "hidden");
        File.WriteAllText(Path.Combine(hiddenFolder, "nested-match.txt"), "nested");
        File.SetAttributes(hiddenFile, File.GetAttributes(hiddenFile) | FileAttributes.Hidden);
        File.SetAttributes(hiddenFolder, File.GetAttributes(hiddenFolder) | FileAttributes.Hidden);
        var all = FolderReader.Read(folder, CancellationToken.None).Items;
        var filtered = FolderReader.Read(folder, CancellationToken.None, false).Items;
        Assert(all.Count == 3 && filtered.Count == 1 && filtered[0].Name == "visible-match.txt", "Hidden entry filtering failed.");
        var searchAll = LocalSearch.Find(folder, "match", CancellationToken.None);
        var searchVisible = LocalSearch.Find(folder, "match", CancellationToken.None, showHiddenItems: false);
        Assert(searchAll.Items.Count == 3 && searchVisible.Items.Count == 1 && searchVisible.Items[0].Path == visible, "Hidden search filtering failed.");
    });
    Check("recursive search and limit", () =>
    {
        var result = LocalSearch.Find(source, "NEEDLE", CancellationToken.None);
        Assert(result.Items.Count == 1 && result.Items[0].Path == Path.Combine(nested, "needle.txt"), "Recursive search failed.");
        Assert(LocalSearch.Find(source, ".txt", CancellationToken.None, 1).LimitReached, "Search limit was not reported.");
    });
    Check("cancellation", () =>
    {
        using var token = new CancellationTokenSource(); token.Cancel();
        Expect<OperationCanceledException>(() => FolderReader.Read(source, token.Token));
        Expect<OperationCanceledException>(() => LocalSearch.Find(source, "x", token.Token));
        Expect<OperationCanceledException>(() => FolderSizeReader.Read(source, token.Token));
    });
    Check("folder size", () => Assert(FolderSizeReader.Read(source, CancellationToken.None).Bytes == 11, "Folder size differs."));
    Check("favorites persistence and deduplication", () =>
    {
        var store = new FavoritesStore(Path.Combine(root, "settings", "favorites.json"));
        Assert(store.Load().Count == 0, "Missing favorites should be empty.");
        store.Save([source, source.ToUpperInvariant(), "relative"]);
        var saved = new FavoritesStore(Path.Combine(root, "settings", "favorites.json")).Load();
        Assert(saved.Count == 1 && saved[0] == source, "Favorites were not preserved or filtered.");
    });
    Check("settings persistence and invalid data protection", () =>
    {
        string path = Path.Combine(root, "settings-check", "settings.json");
        var store = new SettingsStore(path);
        Assert(store.Load().CalculateFolderSizes && store.Load().SidebarWidth == 200, "Defaults differ.");
        store.Save(new(false, 275));
        var loaded = new SettingsStore(path).Load();
        Assert(!loaded.CalculateFolderSizes && loaded.SidebarWidth == 275, "Settings did not persist.");
        File.WriteAllText(path, "{\"CalculateFolderSizes\":false,\"SidebarWidth\":220}");
        Assert(store.Load().ShowHiddenItems, "Existing settings must preserve the previous hidden-item behavior.");
        store.Save(new(false, 220, false));
        Assert(!store.Load().ShowHiddenItems, "Hidden-item preference did not persist.");
        File.WriteAllText(path, "not json");
        Expect<System.Text.Json.JsonException>(() => store.Load());
        Assert(File.ReadAllText(path) == "not json", "Invalid settings were overwritten.");
    });
    Check("breadcrumbs and sorting", () =>
    {
        var crumbs = BreadcrumbBuilder.Build(nested);
        Assert(crumbs.Count > 1 && crumbs[^1].Path == nested && crumbs[^1].Name == "nested", "Breadcrumbs do not reach the folder.");
        var folder = new FolderItem("folder", nested, true, "Folder", null, DateTime.MinValue);
        var file = new FolderItem("file", Path.Combine(source, "alpha.txt"), false, ".txt", 5, DateTime.MinValue);
        Assert(new FolderItemComparer("Name", true).Compare(folder, file) < 0, "Folders must precede files in descending order.");
    });
    Check("copy, conflict and move", () =>
    {
        string file = Path.Combine(source, "alpha.txt");
        FileOperations.Transfer(file, destination, false, false);
        string copy = Path.Combine(destination, "alpha.txt");
        Assert(File.ReadAllText(copy) == "alpha" && File.Exists(file), "Copy failed.");
        Expect<IOException>(() => FileOperations.Transfer(file, destination, false, false));
        Assert(File.ReadAllText(copy) == "alpha" && File.Exists(file), "Conflict changed data.");
        FileOperations.Transfer(file, source, false, false);
        Assert(File.Exists(Path.Combine(source, "alpha - Copy.txt")), "Same-folder copy missing.");
        string moveDestination = Directory.CreateDirectory(Path.Combine(root, "move-destination")).FullName;
        FileOperations.Transfer(file, moveDestination, true, false);
        Assert(!File.Exists(file) && File.ReadAllText(Path.Combine(moveDestination, "alpha.txt")) == "alpha", "Move failed.");
    });
    Check("reject transfer into itself", () => Expect<IOException>(() => FileOperations.Transfer(source, nested, false, false)));
    Check("directory copy and conflict preserve contents", () =>
    {
        string input = Directory.CreateDirectory(Path.Combine(root, "tree-source", "tree")).FullName;
        string child = Directory.CreateDirectory(Path.Combine(input, "child")).FullName;
        File.WriteAllText(Path.Combine(child, "data.txt"), "original");
        string target = Directory.CreateDirectory(Path.Combine(root, "tree-target")).FullName;
        FileOperations.Transfer(input, target, false, false);
        string copied = Path.Combine(target, "tree", "child", "data.txt");
        Assert(File.ReadAllText(copied) == "original" && File.Exists(Path.Combine(child, "data.txt")), "Directory copy lost data.");
        File.WriteAllText(copied, "existing");
        Expect<IOException>(() => FileOperations.Transfer(input, target, false, false));
        Assert(File.ReadAllText(copied) == "existing" && File.ReadAllText(Path.Combine(child, "data.txt")) == "original", "Directory conflict changed data.");
    });
    Check("move conflict and same-folder move preserve contents", () =>
    {
        string from = Directory.CreateDirectory(Path.Combine(root, "move-conflict-source")).FullName;
        string to = Directory.CreateDirectory(Path.Combine(root, "move-conflict-target")).FullName;
        string sourceFile = Path.Combine(from, "conflict.txt");
        string targetFile = Path.Combine(to, "conflict.txt");
        File.WriteAllText(sourceFile, "source"); File.WriteAllText(targetFile, "target");
        Expect<IOException>(() => FileOperations.Transfer(sourceFile, to, true, false));
        Expect<IOException>(() => FileOperations.Transfer(sourceFile, from, true, false));
        Assert(File.ReadAllText(sourceFile) == "source" && File.ReadAllText(targetFile) == "target", "Move conflict changed data.");
    });
    Check("repeated same-folder copies preserve existing names", () =>
    {
        string folder = Directory.CreateDirectory(Path.Combine(root, "copy-names")).FullName;
        string original = Path.Combine(folder, "report.txt");
        File.WriteAllText(original, "original");
        File.WriteAllText(Path.Combine(folder, "report - Copy.txt"), "existing");
        Directory.CreateDirectory(Path.Combine(folder, "report - Copy (2).txt"));
        FileOperations.Transfer(original, folder, false, false);
        Assert(File.ReadAllText(original) == "original", "Original changed.");
        Assert(File.ReadAllText(Path.Combine(folder, "report - Copy.txt")) == "existing", "Existing copy changed.");
        Assert(Directory.Exists(Path.Combine(folder, "report - Copy (2).txt")), "Existing directory changed.");
        Assert(File.ReadAllText(Path.Combine(folder, "report - Copy (3).txt")) == "original", "Next available copy missing.");
    });
    Check("file and folder name conflicts preserve both sides", () =>
    {
        string from = Directory.CreateDirectory(Path.Combine(root, "type-source")).FullName;
        string to = Directory.CreateDirectory(Path.Combine(root, "type-target")).FullName;
        string file = Path.Combine(from, "file-conflict");
        File.WriteAllText(file, "source file");
        string existingFolder = Directory.CreateDirectory(Path.Combine(to, "file-conflict")).FullName;
        File.WriteAllText(Path.Combine(existingFolder, "keep.txt"), "target folder");
        Expect<IOException>(() => FileOperations.Transfer(file, to, false, false));
        Assert(File.ReadAllText(file) == "source file" && File.ReadAllText(Path.Combine(existingFolder, "keep.txt")) == "target folder", "File-to-folder conflict changed data.");
        string folder = Directory.CreateDirectory(Path.Combine(from, "folder-conflict")).FullName;
        File.WriteAllText(Path.Combine(folder, "keep.txt"), "source folder");
        string existingFile = Path.Combine(to, "folder-conflict");
        File.WriteAllText(existingFile, "target file");
        Expect<IOException>(() => FileOperations.Transfer(folder, to, false, false));
        Assert(File.ReadAllText(Path.Combine(folder, "keep.txt")) == "source folder" && File.ReadAllText(existingFile) == "target file", "Folder-to-file conflict changed data.");
    });
    Check("missing source does not modify destination", () =>
    {
        string target = Directory.CreateDirectory(Path.Combine(root, "missing-target")).FullName;
        string sentinel = Path.Combine(target, "keep.txt");
        File.WriteAllText(sentinel, "keep");
        Expect<FileNotFoundException>(() => FileOperations.Transfer(Path.Combine(root, "missing.txt"), target, true, false));
        Assert(Directory.GetFileSystemEntries(target).Length == 1 && File.ReadAllText(sentinel) == "keep", "Missing source changed destination.");
    });
    Check("drive roots cannot be transferred or recycled", () =>
    {
        string driveRoot = Path.GetPathRoot(root)!;
        Expect<IOException>(() => FileOperations.Transfer(driveRoot, destination, false, false));
        Expect<IOException>(() => FileOperations.Recycle(driveRoot));
    });

    if (args.Contains("--benchmark"))
    {
        int count = benchmarkCount;
        string dataset = Directory.CreateDirectory(Path.Combine(root, "benchmark")).FullName;
        for (int i = 0; i < count; i++) File.WriteAllText(Path.Combine(dataset, $"item-{i:D6}.txt"), "test");
        Console.WriteLine($"BENCH environment: {System.Runtime.InteropServices.RuntimeInformation.OSDescription}; {System.Runtime.InteropServices.RuntimeInformation.FrameworkDescription}; {System.Runtime.InteropServices.RuntimeInformation.ProcessArchitecture}; {count} files");
        Measure($"folder listing, {count} files", () => FolderReader.Read(dataset, CancellationToken.None));
        Measure($"local search, {count} files", () => LocalSearch.Find(dataset, $"item-{count - 1:D6}.txt", CancellationToken.None));
    }
}
finally
{
    try { Directory.Delete(root, true); }
    catch (Exception ex) { Console.Error.WriteLine($"Could not remove test directory {root}: {ex.Message}"); failures++; }
}
return failures == 0 ? 0 : 1;

void Check(string name, Action action)
{
    try { action(); Console.WriteLine($"PASS {name}"); }
    catch (Exception ex) { failures++; Console.Error.WriteLine($"FAIL {name}: {ex.Message}"); }
}
static void Assert(bool condition, string message) { if (!condition) throw new InvalidOperationException(message); }
static void Expect<T>(Action action) where T : Exception
{
    try { action(); } catch (T) { return; }
    throw new InvalidOperationException($"Expected {typeof(T).Name}.");
}
static void Measure(string name, Action action)
{
    action();
    var times = new List<double>();
    for (int i = 0; i < 5; i++) { var watch = Stopwatch.StartNew(); action(); watch.Stop(); times.Add(watch.Elapsed.TotalMilliseconds); }
    times.Sort();
    Console.WriteLine($"BENCH {name}: median {times[2]:F2} ms, range {times[0]:F2}–{times[4]:F2} ms (5 warm runs)");
}
