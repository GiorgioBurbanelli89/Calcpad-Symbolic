# Calcpad Server MSI Installer

This project includes a complete MSI installer built with WiX Toolset v5.x for the Calcpad.Server Windows tray application.

## Prerequisites

### For Building the Installer:
- **Windows 10/11** (MSI can only be built on Windows)
- **.NET 8.0 SDK** or later
- **WiX Toolset v5.x** (automatically installed by the build script)
- **PowerShell** (Windows PowerShell or PowerShell Core)

### For End Users:
- **Windows 10/11** (x64)
- **Administrator privileges** (for installation)

## Building the Installer

### Option 1: Using the Batch File (Easiest)
```cmd
# From the Calcpad.Server directory
build-installer.bat
```

### Option 2: Using PowerShell Directly
```powershell
# From the Calcpad.Server directory
.\scripts\build-installer.ps1
```

### Build Options
```powershell
# Build in Debug mode
.\scripts\build-installer.ps1 -Configuration Debug

# Skip application build (use existing published files)
.\scripts\build-installer.ps1 -SkipBuild

# Don't open Explorer after build
.\scripts\build-installer.ps1 -OpenExplorer:$false
```

## What the Installer Does

### During Installation:
1. **Installs to Program Files**: `C:\Program Files\Calcpad\Server\`
2. **Creates shortcuts**: Start Menu and optionally Desktop
3. **Sets up auto-start**: Optionally starts with Windows
4. **Registry entries**: Tracks installation and settings
5. **Environment variables**: Sets installation path

### Application Features:
- **System tray application** with context menu
- **Web server** running on port 9421 (configurable)
- **PDF generation** with automatic browser detection
- **Settings dialog** for port and browser path configuration
- **Auto-start capability** (configurable during install)
- **Logging** to `%LOCALAPPDATA%\Calcpad\` folder

## File Structure

```
Calcpad.Server/
├── CalcpadServer.wxs          # WiX source file (installer definition)
├── CalcpadServer.wixproj      # WiX project file
├── build-installer.bat        # Windows batch file to build installer
├── scripts/
│   ├── build-installer.ps1    # PowerShell build script
│   └── build-windows-tray.sh  # Linux build script (for exe only)
├── calcpad.ico               # Application icon
└── bin/Release/              # Generated MSI installer location
    └── CalcpadServerInstaller.msi
```

## Installer Features

### User Options During Install:
- **Installation directory** (default: Program Files\Calcpad\Server)
- **Desktop shortcut** (optional)
- **Auto-start with Windows** (optional)

### Registry Entries Created:
- `HKLM\SOFTWARE\Calcpad\Server\` - Installation tracking
- `HKCU\SOFTWARE\Microsoft\Windows\CurrentVersion\Run\CalcpadServer` - Auto-start (if enabled)

### Uninstall Support:
- Standard Windows Add/Remove Programs
- Removes all files, shortcuts, and registry entries
- Preserves user data and logs

## Usage After Installation

1. **Start Menu**: "Calcpad Server" shortcut
2. **System Tray**: Look for the "C" icon
3. **Web Access**: http://localhost:9421
4. **Settings**: Right-click tray icon → Settings
5. **Logs**: Right-click tray icon → Open Log File

## Troubleshooting

### Build Issues:
- **WiX not found**: The build script will auto-install WiX toolset
- **Permission errors**: Run PowerShell as Administrator
- **Missing files**: Ensure `calcpad.ico` and `testing/sample-client.html` exist

### Installation Issues:
- **Admin required**: MSI requires Administrator privileges
- **Port conflicts**: Change port in Settings dialog after install
- **Browser not found**: Use Settings dialog to specify browser path

### Runtime Issues:
- **Service won't start**: Check port conflicts, firewall settings
- **PDF generation fails**: Verify browser installation or set custom path
- **Tray icon missing**: Check Windows notification area settings

## Customization

### Modifying the Installer:
1. Edit `CalcpadServer.wxs` for installer behavior
2. Update `CalcpadServer.wixproj` for build settings
3. Modify `build-installer.ps1` for build process

### Common Customizations:
- **Default port**: Change in `CalcpadServer.wxs` and `Program.cs`
- **Installation path**: Modify `INSTALLFOLDER` in WiX file
- **Auto-start default**: Change `AUTO_START` property value
- **Additional files**: Add to `ProductComponents` ComponentGroup

## Distribution

The generated `CalcpadServerInstaller.msi` file can be:
- **Distributed directly** to end users
- **Deployed via Group Policy** in enterprise environments
- **Packaged with other software** using deployment tools
- **Digitally signed** for enhanced security (requires code signing certificate)

## Version Management

Version information is controlled by:
- `ProductVersion` variable in `CalcpadServer.wxs`
- Automatic upgrade detection via `UpgradeCode`
- Registry version tracking for troubleshooting