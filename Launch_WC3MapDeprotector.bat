@echo off

:: Check if .NET 8 is installed
set dotnetFound=false

for /f "delims=" %%i in ('dotnet --list-runtimes') do (
    echo %%i | findstr /c:"WindowsDesktop" >nul 2>&1 && echo %%i | findstr /c:" 8." >nul 2>&1 && set "dotnetFound=true"
)

:: If not found, open the download page in Chrome
if "%dotnetFound%" == "false" (
    echo Please install .NET 8 before running WC3MapDeprotector. Redirecting to download page...
    start "" "https://dotnet.microsoft.com/en-us/download/dotnet/thank-you/runtime-desktop-8.0.0-windows-x64-installer"
    pause
)

cd v1.2.8.0
start WC3MapDeprotector.exe
