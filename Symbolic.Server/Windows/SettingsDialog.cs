#if WINDOWS
using System.Drawing;
using System.Windows.Forms;

namespace Calcpad.Server
{
    public partial class SettingsDialog : Form
    {
        private NumericUpDown _portNumericUpDown;
        private TextBox _browserPathTextBox;
        private Button _browseButton;
        private Button _okButton;
        private Button _cancelButton;
        private Label _statusLabel;
        private Label _browserStatusLabel;
        
        public int SelectedPort { get; private set; }
        public string? CustomBrowserPath { get; private set; }
        
        public SettingsDialog(int currentPort, string? currentBrowserPath = null)
        {
            // Read current values from environment variables if available
            var envPort = Environment.GetEnvironmentVariable("CALCPAD_PORT");
            if (int.TryParse(envPort, out var parsedPort))
            {
                currentPort = parsedPort;
            }
            
            var envBrowserPath = Environment.GetEnvironmentVariable("CALCPAD_BROWSER_PATH");
            if (!string.IsNullOrEmpty(envBrowserPath))
            {
                currentBrowserPath = envBrowserPath;
            }
            
            SelectedPort = currentPort;
            CustomBrowserPath = currentBrowserPath;
            InitializeComponent();
            _portNumericUpDown.Value = currentPort;
            if (!string.IsNullOrEmpty(currentBrowserPath))
            {
                _browserPathTextBox.Text = currentBrowserPath;
            }
            else
            {
                // Auto-detect browser if no custom path is set
                AutoDetectBrowser();
            }
            UpdateStatus();
            UpdateBrowserStatus();
        }
        
        private void InitializeComponent()
        {
            this.Text = "Calcpad Server Settings";
            this.Size = new Size(500, 320);
            this.StartPosition = FormStartPosition.CenterScreen;
            this.FormBorderStyle = FormBorderStyle.FixedDialog;
            this.MaximizeBox = false;
            this.MinimizeBox = false;
            this.ShowIcon = false;
            
            // Port label and input
            var portLabel = new Label
            {
                Text = "Server Port:",
                Location = new Point(20, 30),
                Size = new Size(80, 23),
                TextAlign = ContentAlignment.MiddleLeft
            };
            
            _portNumericUpDown = new NumericUpDown
            {
                Location = new Point(110, 30),
                Size = new Size(100, 23),
                Minimum = 1024,
                Maximum = 65535,
                Value = 9420
            };
            _portNumericUpDown.ValueChanged += PortNumericUpDown_ValueChanged;
            
            // Status label
            _statusLabel = new Label
            {
                Location = new Point(20, 60),
                Size = new Size(450, 20),
                ForeColor = Color.Green,
                Text = "Port is available"
            };
            
            // Browser path section
            var browserLabel = new Label
            {
                Text = "Custom Browser Path (optional):",
                Location = new Point(20, 100),
                Size = new Size(200, 23),
                TextAlign = ContentAlignment.MiddleLeft
            };
            
            _browserPathTextBox = new TextBox
            {
                Location = new Point(20, 125),
                Size = new Size(350, 23),
                PlaceholderText = "Leave empty to auto-detect browser"
            };
            _browserPathTextBox.TextChanged += BrowserPathTextBox_TextChanged;
            
            _browseButton = new Button
            {
                Text = "Browse...",
                Location = new Point(380, 125),
                Size = new Size(80, 23)
            };
            _browseButton.Click += BrowseButton_Click;
            
            // Browser status label
            _browserStatusLabel = new Label
            {
                Location = new Point(20, 155),
                Size = new Size(450, 40),
                ForeColor = Color.Blue,
                Text = "Auto-detecting browser..."
            };
            
            // Auto-detect button
            var autoDetectButton = new Button
            {
                Text = "Auto-Detect",
                Location = new Point(20, 200),
                Size = new Size(100, 23)
            };
            autoDetectButton.Click += AutoDetectButton_Click;
            
            // Clear button
            var clearButton = new Button
            {
                Text = "Clear",
                Location = new Point(130, 200),
                Size = new Size(75, 23)
            };
            clearButton.Click += ClearButton_Click;
            
            // Buttons
            _okButton = new Button
            {
                Text = "OK",
                Location = new Point(300, 250),
                Size = new Size(75, 23),
                DialogResult = DialogResult.OK
            };
            _okButton.Click += OkButton_Click;
            
            _cancelButton = new Button
            {
                Text = "Cancel",
                Location = new Point(385, 250),
                Size = new Size(75, 23),
                DialogResult = DialogResult.Cancel
            };
            
            // Add controls
            this.Controls.AddRange(new Control[] {
                portLabel,
                _portNumericUpDown,
                _statusLabel,
                browserLabel,
                _browserPathTextBox,
                _browseButton,
                _browserStatusLabel,
                autoDetectButton,
                clearButton,
                _okButton,
                _cancelButton
            });
            
            this.AcceptButton = _okButton;
            this.CancelButton = _cancelButton;
        }
        
        private void PortNumericUpDown_ValueChanged(object? sender, EventArgs e)
        {
            UpdateStatus();
        }
        
        private void UpdateStatus()
        {
            var port = (int)_portNumericUpDown.Value;
            
            try
            {
                if (IsPortAvailable(port))
                {
                    // Check if it's actually our server using the port
                    if (IsPortInUse(port) && IsCalcpadServer(port))
                    {
                        _statusLabel.Text = "✓ Port is used by current Calcpad server";
                        _statusLabel.ForeColor = Color.Green;
                    }
                    else
                    {
                        _statusLabel.Text = "✓ Port is available";
                        _statusLabel.ForeColor = Color.Green;
                    }
                    _okButton.Enabled = true;
                }
                else
                {
                    _statusLabel.Text = "⚠ Port is in use by another application";
                    _statusLabel.ForeColor = Color.Orange;
                    _okButton.Enabled = true; // Still allow, user might want to try anyway
                }
            }
            catch (Exception ex)
            {
                _statusLabel.Text = $"❌ Error checking port: {ex.Message}";
                _statusLabel.ForeColor = Color.Red;
                _okButton.Enabled = false;
            }
        }
        
        private void BrowserPathTextBox_TextChanged(object? sender, EventArgs e)
        {
            UpdateBrowserStatus();
        }
        
        private void BrowseButton_Click(object? sender, EventArgs e)
        {
            using var openFileDialog = new OpenFileDialog
            {
                Title = "Select Browser Executable",
                Filter = "Executable Files (*.exe)|*.exe|All Files (*.*)|*.*",
                FilterIndex = 1,
                CheckFileExists = true
            };
            
            // Set initial directory to common browser locations
            var programFiles = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles);
            var programFilesX86 = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86);
            
            if (Directory.Exists(Path.Combine(programFiles, "Microsoft", "Edge", "Application")))
            {
                openFileDialog.InitialDirectory = Path.Combine(programFiles, "Microsoft", "Edge", "Application");
            }
            else if (Directory.Exists(Path.Combine(programFilesX86, "Microsoft", "Edge", "Application")))
            {
                openFileDialog.InitialDirectory = Path.Combine(programFilesX86, "Microsoft", "Edge", "Application");
            }
            else if (Directory.Exists(Path.Combine(programFiles, "Google", "Chrome", "Application")))
            {
                openFileDialog.InitialDirectory = Path.Combine(programFiles, "Google", "Chrome", "Application");
            }
            else if (Directory.Exists(Path.Combine(programFilesX86, "Google", "Chrome", "Application")))
            {
                openFileDialog.InitialDirectory = Path.Combine(programFilesX86, "Google", "Chrome", "Application");
            }
            
            if (openFileDialog.ShowDialog() == DialogResult.OK)
            {
                _browserPathTextBox.Text = openFileDialog.FileName;
                UpdateBrowserStatus();
            }
        }
        
        private void AutoDetectButton_Click(object? sender, EventArgs e)
        {
            AutoDetectBrowser();
        }
        
        private void AutoDetectBrowser()
        {
            _browserStatusLabel.Text = "Detecting browser...";
            _browserStatusLabel.ForeColor = Color.Blue;
            
            try
            {
                var detectedPath = DetectBrowser();
                if (!string.IsNullOrEmpty(detectedPath))
                {
                    _browserPathTextBox.Text = detectedPath;
                    UpdateBrowserStatus();
                }
                else
                {
                    _browserStatusLabel.Text = "❌ No browser found in common locations";
                    _browserStatusLabel.ForeColor = Color.Red;
                }
            }
            catch (Exception ex)
            {
                _browserStatusLabel.Text = $"❌ Error detecting browser: {ex.Message}";
                _browserStatusLabel.ForeColor = Color.Red;
            }
        }
        
        private void ClearButton_Click(object? sender, EventArgs e)
        {
            _browserPathTextBox.Text = "";
            UpdateBrowserStatus();
        }
        
        private void OkButton_Click(object? sender, EventArgs e)
        {
            SelectedPort = (int)_portNumericUpDown.Value;
            CustomBrowserPath = string.IsNullOrWhiteSpace(_browserPathTextBox.Text) ? null : _browserPathTextBox.Text.Trim();
            
            // Set environment variables for the application
            Environment.SetEnvironmentVariable("CALCPAD_PORT", SelectedPort.ToString());
            Environment.SetEnvironmentVariable("CALCPAD_HOST", "localhost");
            
            FileLogger.LogInfo("User selected new port and browser path", $"Port: {SelectedPort}, Browser: {CustomBrowserPath ?? "auto-detect"}");
        }
        
        private void UpdateBrowserStatus()
        {
            var browserPath = _browserPathTextBox.Text.Trim();
            
            if (string.IsNullOrEmpty(browserPath))
            {
                _browserStatusLabel.Text = "ℹ Will auto-detect browser at runtime";
                _browserStatusLabel.ForeColor = Color.Blue;
                return;
            }
            
            try
            {
                if (File.Exists(browserPath))
                {
                    var fileName = Path.GetFileName(browserPath).ToLower();
                    if (fileName.Contains("msedge") || fileName.Contains("chrome") || fileName.Contains("chromium"))
                    {
                        _browserStatusLabel.Text = $"✓ Valid browser: {Path.GetFileName(browserPath)}";
                        _browserStatusLabel.ForeColor = Color.Green;
                    }
                    else
                    {
                        _browserStatusLabel.Text = $"⚠ Unknown browser type: {Path.GetFileName(browserPath)}";
                        _browserStatusLabel.ForeColor = Color.Orange;
                    }
                }
                else
                {
                    _browserStatusLabel.Text = "❌ Browser file not found";
                    _browserStatusLabel.ForeColor = Color.Red;
                }
            }
            catch (Exception ex)
            {
                _browserStatusLabel.Text = $"❌ Error checking browser: {ex.Message}";
                _browserStatusLabel.ForeColor = Color.Red;
            }
        }
        
        private string? DetectBrowser()
        {
            // Check common Windows browser locations
            var browserPaths = new[]
            {
                @"C:\Program Files (x86)\Microsoft\Edge\Application\msedge.exe",
                @"C:\Program Files\Microsoft\Edge\Application\msedge.exe",
                @"C:\Program Files\Google\Chrome\Application\chrome.exe",
                @"C:\Program Files (x86)\Google\Chrome\Application\chrome.exe"
            };
            
            foreach (var path in browserPaths)
            {
                if (File.Exists(path))
                {
                    return path;
                }
            }
            
            return null;
        }
        
        private bool IsPortAvailable(int port)
        {
            try
            {
                using var client = new System.Net.Sockets.TcpClient();
                var result = client.BeginConnect("127.0.0.1", port, null, null);
                var success = result.AsyncWaitHandle.WaitOne(TimeSpan.FromMilliseconds(100));
                
                if (!success)
                {
                    return true; // Port is available (connection timed out)
                }
                
                try
                {
                    client.EndConnect(result);
                    
                    // Port is in use, but check if it's our Calcpad server
                    if (IsCalcpadServer(port))
                    {
                        return true; // It's our server, so we can restart it
                    }
                    
                    return false; // Port is in use by another application
                }
                catch
                {
                    return true; // Port is available (connection failed)
                }
            }
            catch
            {
                return true; // Assume available if we can't check
            }
        }
        
        private bool IsPortInUse(int port)
        {
            try
            {
                using var client = new System.Net.Sockets.TcpClient();
                var result = client.BeginConnect("127.0.0.1", port, null, null);
                var success = result.AsyncWaitHandle.WaitOne(TimeSpan.FromMilliseconds(100));
                
                if (!success)
                {
                    return false; // Port is not in use (connection timed out)
                }
                
                try
                {
                    client.EndConnect(result);
                    return true; // Port is in use (connection succeeded)
                }
                catch
                {
                    return false; // Port is not in use (connection failed)
                }
            }
            catch
            {
                return false; // Assume not in use if we can't check
            }
        }
        
        private bool IsCalcpadServer(int port)
        {
            try
            {
                using var httpClient = new HttpClient();
                httpClient.Timeout = TimeSpan.FromSeconds(2);
                var response = httpClient.GetAsync($"http://localhost:{port}/api/calcpad/sample").Result;
                return response.IsSuccessStatusCode;
            }
            catch
            {
                return false; // Not our server or not responding
            }
        }
    }
}
#endif