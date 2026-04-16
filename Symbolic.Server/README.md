# Calcpad Server

A cross-platform API server for Calcpad mathematical calculations that provides both Windows tray application and Linux console modes.

## Features

- **Cross-Platform**: Runs as a Windows tray application or Linux console application
- **REST API**: Convert Calcpad code to HTML/PDF with configurable settings
- **PDF Generation**: Integrated PDF export using PuppeteerSharp with browser detection
- **Docker Support**: Linux-compatible Docker container with Chromium
- **Web Interface**: Built-in sample client for testing

## Project Structure

```
Calcpad.Server/
├── Core/                           # Shared business logic
│   ├── Calcpad.Server.Core.csproj  # Core project file
│   ├── Services/                   # Business services
│   │   ├── CalcpadApiService.cs    # API configuration
│   │   ├── CalcpadService.cs       # Calcpad integration
│   │   └── PdfGeneratorService.cs  # PDF generation with PuppeteerSharp
│   ├── Controllers/                # API controllers
│   │   └── CalcpadController.cs    # REST endpoints
│   ├── FileLogger.cs               # File logging utility
│   └── template.html               # HTML template
├── Windows/                        # Windows-specific build
│   ├── Calcpad.Server.Windows.csproj # Windows project
│   ├── Program.cs                  # Windows entry point
│   ├── TrayApplication.cs          # System tray functionality
│   ├── SettingsDialog.cs           # Configuration dialog
│   └── calcpad.ico                 # Application icon
├── Linux/                          # Linux-specific build
│   ├── Calcpad.Server.Linux.csproj # Linux project
│   └── Program.cs                  # Linux console entry point
├── scripts/                        # Build automation
│   ├── build-windows-tray.sh       # Build Windows tray app
│   ├── build-linux-console.sh      # Build Linux console app
│   ├── build-installer.ps1         # Create Windows MSI installer
│   └── Commands.md                 # Build command documentation
├── testing/                        # Test files
│   └── sample-client.html          # Web interface for testing
├── docker-compose.yml              # Docker configuration
└── Dockerfile                      # Container definition
```

## Running the Server

### Windows (Standalone Executable)
**Option 1: Build and run the Windows tray application**
```bash
# Build Windows tray application
./scripts/build-windows-tray.sh

# Run the executable (from Windows machine)
.\Windows\bin\Release\net8.0-windows\win-x64\publish\Calcpad.Server.exe
```

The server will start as a system tray application with:
- Tray icon for easy access
- Context menu with "Open in Browser", "Settings", "Open Log File", and "Exit" options
- Balloon notifications for status updates and errors
- Double-click tray icon to open browser
- **Port Conflict Handling**: If port 9421 is busy, right-click → Settings to choose a different port
- **Browser Configuration**: Automatic detection of Edge/Chrome, or custom browser path

**Option 2: Using .NET CLI (Development)**
```bash
dotnet run --project Windows/Calcpad.Server.Windows.csproj
```

### Linux (Console Application)
**Build and run:**
```bash
# Build Linux console application
./scripts/build-linux-console.sh

# Run with .NET CLI
dotnet run --project Linux/Calcpad.Server.Linux.csproj
```

The server will start as a console application with:
- Console output showing server status
- Ctrl+C for graceful shutdown
- API documentation available at `/swagger`
- Default port: 8080

### Docker (Linux)
**Basic Docker:**
```bash
docker build -t calcpad-server .
docker run -p 9420:8080 calcpad-server
```

**Docker Compose (Recommended):**
```bash
# Use default settings
docker compose up -d

# Custom port using environment variable
CALCPAD_PORT=9421 docker compose up -d
```

**Environment Variables:**
Create a `.env` file to customize settings:
```env
CALCPAD_PORT=9421
ASPNETCORE_ENVIRONMENT=Production
```

## API Endpoints

### POST /api/calcpad/convert
Convert Calcpad code to HTML with optional settings.

### POST /api/calcpad/pdf
Generate PDF from Calcpad code with optional settings and PDF configuration.

**Request Body:**
```json
{
  "content": "a = 5\nb = 10\nsum = a + b",
  "settings": {
    "math": {
      "decimals": 6,
      "degrees": 0,
      "isComplex": false,
      "substitute": true,
      "formatEquations": true
    },
    "plot": {
      "colorScale": "Rainbow",
      "lightDirection": "NorthWest",
      "shadows": true,
      "vectorGraphics": false
    },
    "units": "m",
    "output": {
      "format": "html"
    }
  }
}
```

### GET /api/calcpad/sample
Get sample Calcpad content for testing.

## Configuration

### URL Configuration
By default, the server runs on `http://localhost:9420`. You can specify a different URL:

```bash
dotnet run "http://localhost:8080"
```

## Port Conflict Resolution

If you encounter the error "address already in use":

### Windows Tray Application:
1. The application will show a warning dialog
2. Right-click the tray icon → **Settings**
3. Choose a different port (e.g., 9421, 9422, etc.)
4. The server will restart automatically with the new port

### Linux/Console:
```bash
# Try different ports
dotnet run "http://localhost:9421"
dotnet run "http://localhost:9422"
```

### Docker:
```bash
# Change host port
docker run -p 9421:8080 calcpad-server

# Or using environment variable
CALCPAD_PORT=9421 docker compose up -d
```

### Settings
The API accepts comprehensive settings for:
- **Math**: Decimal places, angle units, complex numbers, substitution
- **Plot**: Color schemes, lighting, shadows, graphics format
- **Units**: Base unit system
- **Output**: Format (HTML, PDF, DOCX)

## Testing

Open `sample-client.html` in your browser to test the API with a user-friendly interface that includes:
- Example templates (Basic, Engineering, Physics, Complex Numbers)
- Settings configuration UI
- Real-time conversion testing
- Settings JSON inspection

## Error Logging and Debugging

The application includes comprehensive logging to help debug any issues:

### Log File Location
- **Windows Standalone .exe**: Log file is created in the same directory as `Calcpad.Server.exe`
- **Development**: Log file is created in the project directory
- **Fallback**: If the primary location fails, logs are saved to the Desktop

### Log File Format
Log files are named with the current date: `CalcpadServer-YYYYMMDD.log`

### What Gets Logged
- **Application Startup**: Detailed startup sequence and configuration
- **Server Events**: URL configuration, service registration, HTTP pipeline setup
- **API Calls**: Conversion requests and results
- **Errors**: All exceptions with full stack traces
- **Crashes**: Complete crash reports with context information
- **User Actions**: Tray icon interactions, browser launches, shutdown sequence

### Accessing Logs
**Windows Tray Application:**
- Right-click the tray icon
- Select "Open Log File" to view logs in Notepad
- Log file path is also shown in error dialogs

**Console Application (Linux):**
- Log file path is displayed on startup errors
- Use standard text editors to view the log file

### Error Handling
- **Global Exception Handling**: Catches unhandled exceptions across the entire application
- **Task Exception Handling**: Captures background task failures
- **Windows Forms Exception Handling**: Handles UI-specific errors on Windows
- **API Exception Handling**: Detailed logging of Calcpad conversion errors
- **Graceful Error Display**: User-friendly error messages with log file references

## Building

### Build Scripts
The project includes automated build scripts in the `scripts/` folder:

**Windows Tray Application:**
```bash
./scripts/build-windows-tray.sh
```
Creates: `Windows/bin/Release/net8.0-windows/win-x64/publish/Calcpad.Server.exe`

**Linux Console Application:**
```bash
./scripts/build-linux-console.sh
```
Creates: `Linux/bin/Release/net8.0/linux-x64/publish/Calcpad.Server`

**Windows MSI Installer:**
```powershell
.\scripts\build-installer.ps1
```
Creates: `bin/Release/CalcpadServerInstaller.msi`

### Manual Build Commands

**Windows:**
```bash
# Build Windows tray application
dotnet build Windows/Calcpad.Server.Windows.csproj -r win-x64 -c Release
dotnet publish Windows/Calcpad.Server.Windows.csproj -r win-x64 --self-contained true -p:PublishSingleFile=true -c Release
```

**Linux:**
```bash
# Build Linux console application  
dotnet build Linux/Calcpad.Server.Linux.csproj -r linux-x64 -c Release
dotnet publish Linux/Calcpad.Server.Linux.csproj -r linux-x64 --self-contained true -p:PublishSingleFile=true -c Release
```

### Features of Built Applications
**Windows Tray Application:**
- **No .NET Runtime Required**: Self-contained executable
- **System Tray Integration**: Runs in background with tray icon
- **PDF Generation**: Automatic browser detection (Edge/Chrome) for PDF export
- **Settings Dialog**: Port and browser path configuration
- **Comprehensive Logging**: Automatic crash detection and error logging

**Linux Console Application:**
- **Self-Contained**: No .NET runtime installation required
- **Docker Compatible**: Works in containerized environments
- **PDF Generation**: Uses system Chromium for PDF export
- **Console Logging**: Real-time status and error reporting

## Development

The project uses conditional compilation to ensure compatibility:
- Windows Forms code is only compiled on Windows
- Linux builds exclude Windows-specific dependencies
- Docker builds work seamlessly on Linux environments

## Architecture

The project uses a modular architecture with platform-specific builds:

### Core Components
- **Core/Services/CalcpadApiService.cs**: Shared API configuration and startup logic
- **Core/Services/CalcpadService.cs**: Calcpad integration and calculation processing
- **Core/Services/PdfGeneratorService.cs**: PDF generation with PuppeteerSharp and browser detection
- **Core/Controllers/CalcpadController.cs**: REST API endpoints for HTML and PDF conversion
- **Core/FileLogger.cs**: File-based logging with crash detection

### Platform-Specific Components
**Windows:**
- **Windows/Program.cs**: Windows entry point with tray application startup
- **Windows/TrayApplication.cs**: System tray functionality with context menus and settings
- **Windows/SettingsDialog.cs**: Configuration dialog for port and browser path settings

**Linux:**
- **Linux/Program.cs**: Linux console entry point with server hosting

### Build and Deployment
- **scripts/**: Automated build scripts for different platforms
- **Dockerfile**: Linux container with Chromium for PDF generation
- **docker-compose.yml**: Container orchestration with environment variables