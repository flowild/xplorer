@echo off
setlocal
cd /d "%~dp0"
where dotnet >nul 2>&1
if errorlevel 1 goto :missing_dotnet
if not defined NUGET_PACKAGES if exist "%~dp0.dotnet\packages" set "NUGET_PACKAGES=%~dp0.dotnet\packages"
echo Building Xplorer WinUI...
dotnet build "%~dp0WinUI\Xplorer.WinUI.csproj" -p:Platform=x64 --nologo --verbosity quiet
if errorlevel 1 goto :build_failed
start "" /D "%~dp0WinUI\bin\x64\Debug\net10.0-windows10.0.26100.0\win-x64" "%~dp0WinUI\bin\x64\Debug\net10.0-windows10.0.26100.0\win-x64\Xplorer.WinUI.exe"
exit /b 0

:missing_dotnet
echo .NET 10 SDK was not found. Install it and try again.
pause
exit /b 1

:build_failed
echo Build failed. Close any running Xplorer WinUI window and check the errors above.
pause
exit /b 1
