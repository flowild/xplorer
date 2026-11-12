# Testing Xplorer

## Repeatable checks

From the repository root on Windows with .NET 10:

```powershell
dotnet run --project tests/Xplorer.Checks/Xplorer.Checks.csproj
dotnet run --project tests/Xplorer.Checks/Xplorer.Checks.csproj -- --benchmark
dotnet build Xplorer.csproj
dotnet build WinUI/Xplorer.WinUI.csproj -p:Platform=x64
powershell -NoProfile -ExecutionPolicy Bypass -File tests/WinUI-Smoke.ps1
```

The checks are a dependency-free console runner, not a `dotnet test` project. They compile the shared production sources directly and return a nonzero exit code on a failed check or failed cleanup. Each run creates a randomly named directory under the system temporary directory and deletes only that directory afterward. Tests use generated data, never user-selected files. Eleven checks cover folder enumeration and metadata, recursive search and its limit, pre-cancellation, folder sizes, favorites persistence, breadcrumbs and sorting, file and folder copy and file move without dialogs, conflict protection, same-folder copies and moves, rejection of copying a folder into itself, and rejection of operations on a drive root. The drive-root tests stop before any file operation. They do not exercise the Windows file-operation dialogs, clipboard, actual Recycle Bin deletion, permissions, links, network paths, or the UI.

## Performance measurements

`--benchmark` creates 2,000 small files in one temporary folder. After one warm-up it reports the median and range of five warm runs for shared folder enumeration and local name search. Dataset creation is excluded. These are local filesystem and shared-code measurements, not application startup, UI responsiveness, cold-cache performance, Everything performance, or a comparison with another product. Results depend on the machine, storage, background load and filesystem cache; compare runs on the same environment and do not treat them as guarantees. There are no pass/fail speed thresholds yet.

Baseline on 12 November 2026: Windows build 26300, .NET SDK 10.0.403, Debug configuration, 2,000 files: folder listing median 9.75 ms (range 6.07–12.05 ms); local search median 2.25 ms (range 2.04–4.36 ms). These numbers are a single run, not a benchmark across machines.

A repeat run on the same date passed all six checks: folder listing median 9.63 ms (range 8.98–10.79 ms); search median 3.84 ms (range 2.79–15.28 ms). The variation illustrates why one run is insufficient to establish a performance change.

## UI and system coverage still needed

The PowerShell smoke test starts its own hidden WinUI process, uses Windows UI Automation to open a temporary folder and find a generated file, checks the file action buttons, runs and clears a local search, creates a folder and a file, renames the file through the app's dialogs, and opens and closes a tab while checking the last tab has no close button. All nine checks passed on Windows build 26300 on 12 November 2026. It terminates only the process it started and removes its own temporary directory. An interactive desktop and a preceding x64 Debug build are required; this is not a test of visible rendering, native transfer dialogs or distribution.

On a repeat run the smoke test passed again. Running a copy from a location without the expected build returned a nonzero exit code as intended. This checks the missing-build failure only; failures during navigation and cleanup have not been injected or verified.

Further UI automation should cover keyboard focus and shortcuts, tab history, search cancellation and error states. Native file dialogs and clipboard interactions require separate end-to-end tests with temporary data. Test Recycle Bin restoration, permission failures, long paths, links, disconnected/network drives, large folders and behavior on both supported Windows versions separately. Measure actual startup and responsiveness with documented datasets and environments. The experimental resource workaround and distribution remain unvalidated.
