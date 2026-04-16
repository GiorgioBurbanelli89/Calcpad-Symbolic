# Build script for Calcpad.Server Windows Tray Application
# This script builds a self-contained Windows executable that includes
# the PDF generation service with PuppeteerSharp and Windows Forms tray UI

$ErrorActionPreference = "Stop"

# Colors for output
function Write-Color {
    param(
        [string]$Text,
        [string]$Color = "White"
    )
    Write-Host $Text -ForegroundColor $Color
}

Write-Color "Building Calcpad.Server Windows Tray Application..." "Blue"

# Get the directory where this script is located
$ScriptDir = Split-Path -Parent $MyInvocation.MyCommand.Path
$ProjectDir = Split-Path -Parent $ScriptDir

Write-Color "Project directory: $ProjectDir" "Yellow"

# Change to project directory
Set-Location $ProjectDir

# Clean previous builds
Write-Color "Cleaning previous builds..." "Yellow"
if (Test-Path "Windows\bin") {
    Remove-Item -Recurse -Force "Windows\bin"
}
if (Test-Path "Windows\obj") {
    Remove-Item -Recurse -Force "Windows\obj"
}

# Restore dependencies
Write-Color "Restoring NuGet packages..." "Yellow"
dotnet restore "Windows\Calcpad.Server.Windows.csproj"

# Build for Windows x64
Write-Color "Building Windows x64 tray application..." "Yellow"
dotnet build "Windows\Calcpad.Server.Windows.csproj" -r win-x64 -c Release

# Publish self-contained executable
Write-Color "Publishing self-contained Windows executable..." "Yellow"
dotnet publish "Windows\Calcpad.Server.Windows.csproj" -r win-x64 --self-contained true -p:PublishSingleFile=true -c Release

# Check if build was successful
$PublishPath = "$ProjectDir\Windows\bin\Release\net10.0-windows\win-x64\publish"
if (Test-Path "$PublishPath\Calcpad.Server.exe") {
    Write-Color "✓ Build completed successfully!" "Green"
    Write-Color "Windows executable: $PublishPath\Calcpad.Server.exe" "Green"

    # Show file size
    $FileSize = (Get-Item "$PublishPath\Calcpad.Server.exe").Length
    $FileSizeMB = [math]::Round($FileSize / 1MB, 2)
    Write-Color "File size: ${FileSizeMB}MB" "Green"

    # List all files in publish directory
    Write-Color "`nPublished files:" "Blue"
    Get-ChildItem $PublishPath | Format-Table Name, @{Label="Size (MB)"; Expression={[math]::Round($_.Length / 1MB, 2)}}

    Write-Color "`nUsage:" "Green"
    Write-Color "  Standalone: Copy Calcpad.Server.exe to a Windows machine" "Green"
    Write-Color "  Full: Copy the entire publish folder to a Windows machine" "Green"
    Write-Color "  Run: Calcpad.Server.exe [optional_url]" "Green"
    Write-Color "  Example: Calcpad.Server.exe http://localhost:9420" "Green"

    Write-Color "`nFeatures included:" "Yellow"
    Write-Color "  • Windows Forms tray application with system tray icon" "Yellow"
    Write-Color "  • ASP.NET Core web server with REST API" "Yellow"
    Write-Color "  • PDF generation with PuppeteerSharp" "Yellow"
    Write-Color "  • Automatic browser detection (Edge, Chrome)" "Yellow"
    Write-Color "  • Settings dialog for port and browser configuration" "Yellow"
    Write-Color "  • File logging to AppData\Local\Calcpad folder" "Yellow"

} else {
    Write-Color "✗ Build failed - executable not found" "Red"
    Write-Color "Expected path: $PublishPath\Calcpad.Server.exe" "Red"
    exit 1
}
