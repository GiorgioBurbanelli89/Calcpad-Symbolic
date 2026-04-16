# PowerShell script to build Calcpad.Server MSI installer
# Requires WiX Toolset v5.x and .NET 8.0 SDK

param(
    [Parameter()]
    [ValidateSet("Debug", "Release")]
    [string]$Configuration = "Release",
    
    [Parameter()]
    [switch]$SkipBuild = $false,
    
    [Parameter()]
    [switch]$OpenExplorer = $true
)

# Colors for output
$ErrorColor = "Red"
$WarningColor = "Yellow" 
$InfoColor = "Cyan"
$SuccessColor = "Green"

function Write-Info($message) {
    Write-Host $message -ForegroundColor $InfoColor
}

function Write-Success($message) {
    Write-Host $message -ForegroundColor $SuccessColor
}

function Write-Warning($message) {
    Write-Host $message -ForegroundColor $WarningColor
}

function Write-Error($message) {
    Write-Host $message -ForegroundColor $ErrorColor
}

# Get script directory and project root
$ScriptDir = Split-Path -Parent $MyInvocation.MyCommand.Path
$ProjectDir = Split-Path -Parent $ScriptDir
$PublishDir = Join-Path $ProjectDir "Windows\bin\$Configuration\net8.0-windows\win-x64\publish"
$WixProjectPath = Join-Path $ProjectDir "Windows\CalcpadServer.wixproj"

Write-Info "Building Calcpad.Server MSI Installer..."
Write-Info "Project Directory: $ProjectDir"
Write-Info "Configuration: $Configuration"

# Change to project directory
Set-Location $ProjectDir

try {
    # Check if WiX toolset is installed
    Write-Info "Checking WiX Toolset installation..."
    $wixInstalled = $false
    try {
        & dotnet tool list --global | Select-String "wix" | Out-Null
        if ($LASTEXITCODE -eq 0) {
            $wixInstalled = $true
        }
    } catch {
        # Try alternative check
        try {
            & wix --version | Out-Null
            if ($LASTEXITCODE -eq 0) {
                $wixInstalled = $true
            }
        } catch {
            $wixInstalled = $false
        }
    }
    
    if (-not $wixInstalled) {
        Write-Warning "WiX Toolset not found. Installing as global tool..."
        & dotnet tool install --global wix
        if ($LASTEXITCODE -ne 0) {
            throw "Failed to install WiX toolset"
        }
        Write-Success "WiX Toolset installed successfully"
    } else {
        Write-Success "WiX Toolset found"
    }

    if (-not $SkipBuild) {
        # Clean previous builds
        Write-Info "Cleaning previous builds..."
        if (Test-Path "bin") {
            Remove-Item -Recurse -Force "bin"
        }
        if (Test-Path "obj") {
            Remove-Item -Recurse -Force "obj"
        }

        # Restore dependencies
        Write-Info "Restoring NuGet packages..."
        & dotnet restore "Windows\Calcpad.Server.Windows.csproj"
        if ($LASTEXITCODE -ne 0) {
            throw "dotnet restore failed"
        }

        # Build the application
        Write-Info "Building Windows x64 tray application..."
        & dotnet build "Windows\Calcpad.Server.Windows.csproj" -r win-x64 -c $Configuration
        if ($LASTEXITCODE -ne 0) {
            throw "dotnet build failed"
        }

        # Publish self-contained executable
        Write-Info "Publishing self-contained Windows executable..."
        & dotnet publish "Windows\Calcpad.Server.Windows.csproj" -r win-x64 --self-contained true -p:PublishSingleFile=true -p:PublishTrimmed=false -c $Configuration
        if ($LASTEXITCODE -ne 0) {
            throw "dotnet publish failed"
        }

        # Verify the executable was created
        $ExePath = Join-Path $PublishDir "Calcpad.Server.exe"
        if (-not (Test-Path $ExePath)) {
            throw "Published executable not found at: $ExePath"
        }
        
        $FileSize = (Get-Item $ExePath).Length
        $FileSizeMB = [math]::Round($FileSize / 1MB, 2)
        Write-Success "✓ Application built successfully"
        Write-Success "  Executable: $ExePath"
        Write-Success "  Size: $FileSizeMB MB"
    } else {
        Write-Warning "Skipping application build (using existing published files)"
        
        # Verify the executable exists
        $ExePath = Join-Path $PublishDir "Calcpad.Server.exe"
        if (-not (Test-Path $ExePath)) {
            throw "Published executable not found at: $ExePath. Run without -SkipBuild to build the application first."
        }
    }

    # Build the MSI installer
    Write-Info "Building MSI installer..."
    & dotnet build "Windows\CalcpadServer.wixproj" -c $Configuration
    if ($LASTEXITCODE -ne 0) {
        throw "MSI build failed"
    }

    # Find the generated MSI
    $MsiPath = Join-Path $ProjectDir "Windows\bin\$Configuration\CalcpadServerInstaller.msi"
    if (Test-Path $MsiPath) {
        $MsiSize = (Get-Item $MsiPath).Length
        $MsiSizeMB = [math]::Round($MsiSize / 1MB, 2)
        Write-Success "✓ MSI installer created successfully!"
        Write-Success "  Installer: $MsiPath"
        Write-Success "  Size: $MsiSizeMB MB"
        
        # MSI installer available in bin directory (not copied to avoid large git files)
        Write-Success "  MSI installer available at: $MsiPath"
        
        Write-Info ""
        Write-Info "Installation Features:"
        Write-Info "  • Windows Forms tray application with system tray icon"
        Write-Info "  • ASP.NET Core web server with REST API (default port 9421)"
        Write-Info "  • PDF generation with PuppeteerSharp"
        Write-Info "  • Automatic browser detection (Edge, Chrome)"
        Write-Info "  • Start menu shortcut"
        Write-Info "  • Optional desktop shortcut"
        Write-Info "  • Auto-start with Windows (configurable)"
        Write-Info "  • Settings dialog for port and browser configuration"
        
        Write-Info ""
        Write-Info "Usage:"
        Write-Info "  1. Run CalcpadServerInstaller.msi as Administrator"
        Write-Info "  2. Follow the installation wizard"
        Write-Info "  3. The application will start automatically and appear in system tray"
        Write-Info "  4. Access the web interface at http://localhost:9421"
        
        if ($OpenExplorer) {
            Write-Info "Opening Windows Explorer to show the installer..."
            & explorer.exe "/select,$MsiPath"
        }
        
    } else {
        throw "MSI installer not found at expected location: $MsiPath"
    }

} catch {
    Write-Error "Build failed: $($_.Exception.Message)"
    exit 1
}

Write-Success ""
Write-Success "Build completed successfully!"