@echo off
setlocal
cd /d "%~dp0"
where dotnet >nul 2>&1
if errorlevel 1 goto :missing_dotnet
echo Building Xplorer WPF...
dotnet build "%~dp0Xplorer.csproj" --nologo --verbosity quiet
if errorlevel 1 goto :build_failed
start "" /D "%~dp0bin\Debug\net10.0-windows" "%~dp0bin\Debug\net10.0-windows\Xplorer.exe"
exit /b 0

:missing_dotnet
echo .NET 10 SDK was not found. Install it and try again.
pause
exit /b 1

:build_failed
echo Build failed. Close any running Xplorer WPF window and check the errors above.
pause
exit /b 1
