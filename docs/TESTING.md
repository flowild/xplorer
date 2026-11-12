# Testing Xplorer

## Repeatable checks

From the repository root on Windows with .NET 10:

```powershell
dotnet run --project tests/Xplorer.Checks/Xplorer.Checks.csproj
dotnet run --project tests/Xplorer.Checks/Xplorer.Checks.csproj -- --benchmark --count 10000
dotnet build Xplorer.csproj
dotnet build WinUI/Xplorer.WinUI.csproj -p:Platform=x64
powershell -NoProfile -ExecutionPolicy Bypass -File tests/WinUI-Smoke.ps1
powershell -NoProfile -ExecutionPolicy Bypass -File tests/WinUI-NavigationRace.ps1
```

The checks are a dependency-free console runner, not a `dotnet test` project. They compile the shared production sources directly and return a nonzero exit code on a failed check or failed cleanup. Each run creates a randomly named directory under the system temporary directory and deletes only that directory afterward. Tests use generated data, never user-selected files. Sixteen checks cover folder enumeration and metadata, Windows-hidden files and directories in listing and search, recursive search and its limit, pre-cancellation, folder sizes, favorites and settings persistence, breadcrumbs and sorting, file and folder copy and file move without dialogs, conflict protection, same-folder copies and moves, rejection of copying a folder into itself, rejection of operations on a drive root, repeated copy-name collisions, file-versus-directory conflicts, and a missing source without modifying the destination. The drive-root tests stop before any file operation. The three additional conflict and missing-source checks passed on Windows build 26300 on 12 November 2026. They do not exercise the Windows file-operation dialogs, clipboard, actual Recycle Bin deletion, permissions, links, network paths, or the UI.

## Performance measurements

`--benchmark` creates 2,000 small files in one temporary folder by default. `--count N` selects 1 to 100,000 files and requires `--benchmark`; larger datasets take more time and disk space. The runner reports OS, .NET runtime, process architecture and file count. After one warm-up it reports the median and range of five warm runs for shared folder enumeration and local name search. Dataset creation is excluded. These are local filesystem and shared-code measurements, not application startup, UI responsiveness, cold-cache performance, Everything performance, or a comparison with another product. Results depend on the machine, storage, background load and filesystem cache; compare runs on the same environment and do not treat them as guarantees. There are no pass/fail speed thresholds yet.

Baseline on 12 November 2026: Windows build 26300, .NET SDK 10.0.403, Debug configuration, 2,000 files: folder listing median 9.75 ms (range 6.07–12.05 ms); local search median 2.25 ms (range 2.04–4.36 ms). These numbers are a single run, not a benchmark across machines.

A repeat run on the same date passed all six checks: folder listing median 9.63 ms (range 8.98–10.79 ms); search median 3.84 ms (range 2.79–15.28 ms). The variation illustrates why one run is insufficient to establish a performance change.

A 10,000-file run on 12 November 2026 on Windows build 26300, .NET 10.0.14 x64, Debug: listing median 60.27 ms (range 38.76–98.87 ms); search median 10.59 ms (range 9.69–24.39 ms), five warm runs after one warm-up. Eleven functional checks passed. A subsequent 1,000-file run also passed; its listing median was 4.77 ms (4.71–6.81 ms) and search median 2.82 ms (2.54–14.82 ms). The changed filenames and counts mean these are not direct before/after comparisons with the older baseline. Storage type, CPU and background load were not recorded; the measurements do not cover WinUI first paint or input latency.
## UI and system coverage still needed

The PowerShell smoke test starts its own hidden WinUI process, uses Windows UI Automation to open a temporary folder and find a generated file, checks the file action buttons, runs and clears a local search, creates a folder and a file, renames the file through the app's dialogs, and opens and closes a tab while checking the last tab has no close button, and verifies select all, clear selection, the selection count, partial-selection inversion, sort indicator accessibility names and the settings menu. All thirteen checks passed on Windows build 26300 on 12 November 2026. It terminates only the process it started and removes its own temporary directory. An interactive desktop and a preceding x64 Debug build are required; this is not a test of visible rendering, native transfer dialogs or distribution.

On a repeat run the smoke test passed again. Running a copy from a location without the expected build returned a nonzero exit code as intended. This checks the missing-build failure only; failures during navigation and cleanup have not been injected or verified.

`WinUI-NavigationRace.ps1` uses a temporary folder with 3,000 files and a second folder with a marker. It checks rapid successive folder requests, a search immediately followed by navigation, and switching tabs after initiating navigation in another tab. Each check waits for the target and observes it for two more seconds. All three passed on Windows build 26300 on 12 November 2026. Timing is not controlled: operations may finish before the next action, so this is a smoke test and does not prove that an outstanding operation was canceled or exercise every race. It starts and terminates its own hidden app process and removes its own test directory.

Further UI automation should cover keyboard focus and shortcuts, tab history, deterministic in-flight search cancellation and error states. Native file dialogs and clipboard interactions require separate end-to-end tests with temporary data. Test Recycle Bin restoration, permission failures, long paths, links, disconnected/network drives, large folders and behavior on both supported Windows versions separately. Measure actual startup and responsiveness with documented datasets and environments. The experimental resource workaround and distribution remain unvalidated.

On 12 November 2026, the revised WinUI navigation uses Enter and menu flyouts. Both UI scripts require desktop keyboard access; in the restricted agent sandbox SendKeys was denied, while execution with approved desktop access succeeded. A captured application window was visually inspected on build 26300; it does not validate other themes, display sizes or Windows versions. The settings persistence check uses a temporary file and does not change the user's settings.

On 12 November 2026, the hidden-item check passed with temporary files and a hidden directory, including search exclusion and compatibility with earlier settings. The settings control was exposed in UI Automation; changing it and persistence across a GUI restart have not been end-to-end tested.
