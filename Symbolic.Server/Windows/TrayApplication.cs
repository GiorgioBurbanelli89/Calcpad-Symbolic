#if WINDOWS
using System.Drawing;
using System.Windows.Forms;
using Microsoft.AspNetCore.Builder;

namespace Calcpad.Server
{
    public class TrayApplication : Form
    {
        private NotifyIcon? _trayIcon;
        private ContextMenuStrip? _contextMenu;
        private readonly WebApplication _app;
        private string _serverUrl;
        private readonly bool _hasStartupError;
        private string? _customBrowserPath;

        public TrayApplication(WebApplication app, string serverUrl, bool hasStartupError = false)
        {
            try
            {
                FileLogger.LogInfo("Initializing tray application", $"{serverUrl}, HasStartupError: {hasStartupError}");
                _app = app;
                _serverUrl = serverUrl;
                _hasStartupError = hasStartupError;
                
                // Load custom browser path from environment variable or config
                _customBrowserPath = Environment.GetEnvironmentVariable("CALCPAD_BROWSER_PATH");
                
                InitializeTrayIcon();
                
                // Hide the form window
                WindowState = FormWindowState.Minimized;
                ShowInTaskbar = false;
                Visible = false;
                
                if (_hasStartupError)
                {
                    _trayIcon?.ShowBalloonTip(5000, "Calcpad Server", "Server failed to start on configured port. Right-click to change settings.", ToolTipIcon.Warning);
                }
                
                FileLogger.LogInfo("Tray application initialized successfully");
            }
            catch (Exception ex)
            {
                FileLogger.LogCrash(ex, "TrayApplication constructor");
                throw;
            }
        }

        private void InitializeTrayIcon()
        {
            // Create context menu
            _contextMenu = new ContextMenuStrip();
            
            var openLogItem = new ToolStripMenuItem("Open Log File");
            openLogItem.Click += (sender, e) => OpenLogFile();
            
            var settingsItem = new ToolStripMenuItem("Settings...");
            settingsItem.Click += (sender, e) => OpenSettings();
            
            var statusItem = new ToolStripMenuItem($"Server: {_serverUrl}");
            statusItem.Enabled = false;
            
            // Make status item red if there's an error
            if (_hasStartupError)
            {
                statusItem.ForeColor = Color.Red;
            }
            
            var separatorItem = new ToolStripSeparator();
            var separator2Item = new ToolStripSeparator();
            var separator3Item = new ToolStripSeparator();
            
            var exitItem = new ToolStripMenuItem("Exit");
            exitItem.Click += (sender, e) => ExitApplication();
            
            _contextMenu.Items.AddRange(new ToolStripItem[] {
                statusItem,
                separatorItem,
                openLogItem,
                separator2Item,
                settingsItem,
                separator3Item,
                exitItem
            });

            // Create tray icon with a simple generated icon
            _trayIcon = new NotifyIcon()
            {
                Icon = CreateIcon(),
                ContextMenuStrip = _contextMenu,
                Text = "Calcpad Server",
                Visible = true
            };
            
            _trayIcon.DoubleClick += (sender, e) => OpenBrowser();
            
            // Show balloon tip on startup
            _trayIcon.ShowBalloonTip(3000, "Calcpad Server", $"Server started at {_serverUrl}", ToolTipIcon.Info);
        }

        private Icon CreateIcon()
        {
            try
            {
                // Load icon from embedded resources
                var assembly = System.Reflection.Assembly.GetExecutingAssembly();
                var resourceName = "Calcpad.Server.calcpad-server.ico";
                
                using var stream = assembly.GetManifestResourceStream(resourceName);
                if (stream != null)
                {
                    return new Icon(stream);
                }
                
                // Fallback: try to load from file system if embedded resource fails
                var iconPath = Path.Combine(AppContext.BaseDirectory, "calcpad-server.ico");
                if (File.Exists(iconPath))
                {
                    return new Icon(iconPath);
                }
                
                // Last resort: create a simple icon programmatically
                return CreateFallbackIcon();
            }
            catch (Exception ex)
            {
                FileLogger.LogError("Failed to load icon", ex);
                return CreateFallbackIcon();
            }
        }

        private Icon CreateFallbackIcon()
        {
            // Create a simple 16x16 icon programmatically
            var bitmap = new Bitmap(16, 16);
            using (var g = Graphics.FromImage(bitmap))
            {
                g.Clear(Color.Blue);
                g.FillEllipse(Brushes.White, 2, 2, 12, 12);
                g.DrawString("C", new Font("Arial", 8, FontStyle.Bold), Brushes.Blue, 4, 2);
            }
            return Icon.FromHandle(bitmap.GetHicon());
        }

        private void OpenBrowser()
        {
            try
            {
                FileLogger.LogInfo("Opening browser", _serverUrl);
                System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
                {
                    FileName = _serverUrl,
                    UseShellExecute = true
                });
            }
            catch (Exception ex)
            {
                FileLogger.LogError("Failed to open browser", ex);
                var logPath = FileLogger.GetLogFilePath() ?? "Unknown location";
                MessageBox.Show(
                    $"Could not open browser: {ex.Message}\n\n" +
                    $"Server URL: {_serverUrl}\n" +
                    $"Error logged to: {logPath}", 
                    "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void OpenLogFile()
        {
            try
            {
                var logPath = FileLogger.GetLogFilePath();
                if (string.IsNullOrEmpty(logPath) || !File.Exists(logPath))
                {
                    MessageBox.Show("Log file not found or not accessible.", "Log File", MessageBoxButtons.OK, MessageBoxIcon.Information);
                    return;
                }

                FileLogger.LogInfo("Opening log file", logPath);
                System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
                {
                    FileName = "notepad.exe",
                    Arguments = $"\"{logPath}\"",
                    UseShellExecute = true
                });
            }
            catch (Exception ex)
            {
                FileLogger.LogError("Failed to open log file", ex);
                var logPath = FileLogger.GetLogFilePath() ?? "Unknown location";
                MessageBox.Show(
                    $"Could not open log file: {ex.Message}\n\n" +
                    $"Log file location: {logPath}\n\n" +
                    $"You can manually navigate to this location to view the log file.", 
                    "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void OpenSettings()
        {
            try
            {
                FileLogger.LogInfo("Opening settings dialog");
                
                // Extract current port from URL
                var currentPort = 9420;
                if (Uri.TryCreate(_serverUrl, UriKind.Absolute, out var uri))
                {
                    currentPort = uri.Port;
                }
                
                using var settingsDialog = new SettingsDialog(currentPort, _customBrowserPath);
                if (settingsDialog.ShowDialog() == DialogResult.OK)
                {
                    var newPort = settingsDialog.SelectedPort;
                    var newBrowserPath = settingsDialog.CustomBrowserPath;
                    
                    var portChanged = newPort != currentPort;
                    var browserChanged = newBrowserPath != _customBrowserPath;
                    
                    if (browserChanged)
                    {
                        _customBrowserPath = newBrowserPath;
                        // Set environment variable for the PDF service to use
                        Environment.SetEnvironmentVariable("CALCPAD_BROWSER_PATH", _customBrowserPath);
                        FileLogger.LogInfo("Browser path updated", _customBrowserPath ?? "cleared");
                    }
                    
                    if (portChanged)
                    {
                        RestartServerWithNewPort(newPort);
                    }
                    else if (browserChanged)
                    {
                        _trayIcon?.ShowBalloonTip(3000, "Settings Updated", 
                            "Browser path updated. Changes will take effect for new PDF generations.", 
                            ToolTipIcon.Info);
                    }
                }
            }
            catch (Exception ex)
            {
                FileLogger.LogError("Failed to open settings dialog", ex);
                MessageBox.Show($"Failed to open settings: {ex.Message}", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }
        
        private async void RestartServerWithNewPort(int newPort)
        {
            try
            {
                FileLogger.LogInfo("Restarting server with new port", newPort.ToString());
                _trayIcon?.ShowBalloonTip(3000, "Calcpad Server", "Restarting server with new port...", ToolTipIcon.Info);
                
                // Stop the current server
                using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));
                await _app.StopAsync(cts.Token);
                
                var newUrl = $"http://localhost:{newPort}";
                _serverUrl = newUrl;
                
                // Start a new process with the new port
                var processPath = Environment.ProcessPath ?? Path.Combine(AppContext.BaseDirectory, "Calcpad.Server.exe");
                var startInfo = new System.Diagnostics.ProcessStartInfo
                {
                    FileName = processPath,
                    Arguments = newUrl,
                    UseShellExecute = true,
                    WindowStyle = System.Diagnostics.ProcessWindowStyle.Hidden
                };
                
                System.Diagnostics.Process.Start(startInfo);
                
                // Exit this instance
                FileLogger.LogInfo("Started new server instance, exiting current instance");
                Application.Exit();
                Environment.Exit(0);
            }
            catch (Exception ex)
            {
                FileLogger.LogError("Failed to restart server", ex);
                MessageBox.Show(
                    $"Failed to restart server: {ex.Message}\n\n" +
                    $"You may need to manually restart the application with:\n" +
                    $"Calcpad.Server.exe \"http://localhost:{newPort}\"",
                    "Restart Failed", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private async void ExitApplication()
        {
            try
            {
                FileLogger.LogInfo("User requested application exit");
                if (_trayIcon != null) _trayIcon.Visible = false;
                _trayIcon?.ShowBalloonTip(2000, "Calcpad Server", "Shutting down server...", ToolTipIcon.Info);
                
                // Give the server time to shut down gracefully
                FileLogger.LogInfo("Stopping web server");
                using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(10));
                await _app.StopAsync(cts.Token);
                FileLogger.LogInfo("Web server stopped successfully");
            }
            catch (Exception ex)
            {
                FileLogger.LogError("Error during shutdown", ex);
                Console.WriteLine($"Error during shutdown: {ex.Message}");
            }
            finally
            {
                FileLogger.LogInfo("Application exit complete");
                Application.Exit();
                Environment.Exit(0);
            }
        }

        protected override void SetVisibleCore(bool value)
        {
            base.SetVisibleCore(false);
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                _trayIcon?.Dispose();
                _contextMenu?.Dispose();
            }
            base.Dispose(disposing);
        }
    }
}
#endif