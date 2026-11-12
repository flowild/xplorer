using System.Diagnostics;
using Xplorer;

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
    Check("drive roots cannot be transferred or recycled", () =>
    {
        string driveRoot = Path.GetPathRoot(root)!;
        Expect<IOException>(() => FileOperations.Transfer(driveRoot, destination, false, false));
        Expect<IOException>(() => FileOperations.Recycle(driveRoot));
    });

    if (args.Contains("--benchmark"))
    {
        string dataset = Directory.CreateDirectory(Path.Combine(root, "benchmark")).FullName;
        for (int i = 0; i < 2000; i++) File.WriteAllText(Path.Combine(dataset, $"item-{i:D5}.txt"), "test");
        Measure("folder listing, 2000 files", () => FolderReader.Read(dataset, CancellationToken.None));
        Measure("local search, 2000 files", () => LocalSearch.Find(dataset, "item-01999", CancellationToken.None));
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
