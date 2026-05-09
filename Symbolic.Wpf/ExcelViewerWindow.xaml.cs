using System;
using System.IO;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using System.Windows;
using Microsoft.Web.WebView2.Core;

namespace Calcpad.Wpf
{
    public partial class ExcelViewerWindow : Window
    {
        private string _currentFilePath;
        private bool _univerReady;
        private readonly string _logPath;

        public ExcelViewerWindow()
        {
            InitializeComponent();
            _logPath = Path.Combine(Path.GetTempPath(), "calcpad-excel-viewer.log");
            try { File.WriteAllText(_logPath, $"=== Session started {DateTime.Now:O} ===\n"); } catch { }
            Log("ExcelViewerWindow constructor. Log file: " + _logPath);
            _ = InitViewerAsync();
        }

        private void Log(string msg)
        {
            try
            {
                File.AppendAllText(_logPath, $"[{DateTime.Now:HH:mm:ss.fff}] {msg}\n");
            }
            catch { }
            Dispatcher.InvokeAsync(() =>
            {
                StatusLabel.Text = msg.Length > 200 ? msg.Substring(0, 200) + "…" : msg;
            });
        }

        private async Task InitViewerAsync()
        {
            try
            {
                var userDataFolder = Path.Combine(Path.GetTempPath(), "CalcpadExcelViewer");
                Directory.CreateDirectory(userDataFolder);
                Log("WebView2 UserDataFolder: " + userDataFolder);

                var env = await CoreWebView2Environment.CreateAsync(null, userDataFolder);
                Log("CoreWebView2Environment creado");
                await Viewer.EnsureCoreWebView2Async(env);
                Log("EnsureCoreWebView2Async OK");

                Viewer.CoreWebView2.Settings.AreDevToolsEnabled = true;
                Viewer.CoreWebView2.Settings.IsStatusBarEnabled = true;
                Log("DevTools habilitado (F12 dentro del webview)");

                Viewer.CoreWebView2.WebResourceRequested += (s, a) =>
                {
                    // log only errors/navigation
                };
                Viewer.CoreWebView2.NavigationStarting += (s, a) =>
                {
                    Log("NavigationStarting: " + a.Uri);
                };
                Viewer.CoreWebView2.ProcessFailed += (s, a) =>
                {
                    Log("ProcessFailed: " + a.ProcessFailedKind + " exit=" + a.ExitCode);
                };

                var appPath = AppDomain.CurrentDomain.BaseDirectory;
                var htmlPath = Path.Combine(appPath, "excel-viewer", "index.html");
                Log("HTML path: " + htmlPath + " | exists=" + File.Exists(htmlPath));

                if (!File.Exists(htmlPath))
                {
                    Log("ERROR: index.html no encontrado");
                    return;
                }

                var uri = new Uri(htmlPath).AbsoluteUri;
                Log("Navegando a: " + uri);
                Viewer.CoreWebView2.Navigate(uri);
            }
            catch (Exception ex)
            {
                Log("EXCEPTION en Init: " + ex.GetType().Name + ": " + ex.Message + "\n" + ex.StackTrace);
            }
        }

        private void Viewer_NavigationCompleted(object sender, CoreWebView2NavigationCompletedEventArgs e)
        {
            Log($"NavigationCompleted: success={e.IsSuccess} status={e.HttpStatusCode} webErr={e.WebErrorStatus}");
        }

        private void Viewer_WebMessageReceived(object sender, CoreWebView2WebMessageReceivedEventArgs e)
        {
            try
            {
                var msg = e.TryGetWebMessageAsString();
                if (msg == "univer-ready")
                {
                    _univerReady = true;
                    Log("✓ univer-ready recibido del JS");
                    Dispatcher.InvokeAsync(() => SaveBtn.IsEnabled = false);
                }
                else if (msg != null && msg.StartsWith("LOG:"))
                {
                    // Forward JS log to file
                    Log("JS " + msg.Substring(4));
                }
                else
                {
                    Log("WebMessage: " + msg);
                }
            }
            catch (Exception ex)
            {
                Log("WebMessageReceived ERROR: " + ex.Message);
            }
        }

        private async void OpenBtn_Click(object sender, RoutedEventArgs e)
        {
            var dlg = new Microsoft.Win32.OpenFileDialog
            {
                Filter = "Excel (*.xlsx;*.xlsm)|*.xlsx;*.xlsm",
                Title = "Abrir archivo Excel"
            };
            if (dlg.ShowDialog() != true) return;
            await LoadXlsxAsync(dlg.FileName);
        }

        public async Task LoadXlsxAsync(string path)
        {
            try
            {
                if (!_univerReady)
                {
                    Log("WARN: LoadXlsx llamado antes de univer-ready. Esperando 3s y reintentando…");
                    await Task.Delay(3000);
                    if (!_univerReady)
                    {
                        Log("ERROR: Univer sigue sin estar listo. Revisar logs JS arriba.");
                        return;
                    }
                }

                _currentFilePath = path;
                FileNameLabel.Text = Path.GetFileName(path);
                Log($"Leyendo xlsx: {path} ({new FileInfo(path).Length} bytes)");

                byte[] bytes;
                using (var fs = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete))
                using (var ms = new MemoryStream())
                {
                    await fs.CopyToAsync(ms);
                    bytes = ms.ToArray();
                }
                var b64 = Convert.ToBase64String(bytes);
                Log($"Base64 length: {b64.Length}. Invocando calcpadLoadXlsx…");

                var b64Json = JsonSerializer.Serialize(b64);
                var nameJson = JsonSerializer.Serialize(Path.GetFileName(path));
                var script = $"window.calcpadLoadXlsx({b64Json}, {nameJson})";
                var result = await Viewer.CoreWebView2.ExecuteScriptAsync(script);
                Log("calcpadLoadXlsx retornó: " + (result.Length > 300 ? result.Substring(0, 300) + "…" : result));

                SaveBtn.IsEnabled = true;
            }
            catch (Exception ex)
            {
                Log("LoadXlsx EXCEPTION: " + ex.Message + "\n" + ex.StackTrace);
            }
        }

        private async void SaveBtn_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                var dlg = new Microsoft.Win32.SaveFileDialog
                {
                    Filter = "Excel (*.xlsx)|*.xlsx",
                    Title = "Guardar archivo Excel",
                    FileName = _currentFilePath != null ? Path.GetFileName(_currentFilePath) : "documento.xlsx"
                };
                if (dlg.ShowDialog() != true) return;

                Log("Exportando…");
                var result = await Viewer.CoreWebView2.ExecuteScriptAsync("window.calcpadExportXlsx()");
                if (string.IsNullOrEmpty(result) || result == "null")
                {
                    Log("ERROR: export retornó null");
                    return;
                }

                var b64 = JsonSerializer.Deserialize<string>(result);
                if (string.IsNullOrEmpty(b64)) { Log("ERROR: export vacío"); return; }

                var bytes = Convert.FromBase64String(b64);
                await File.WriteAllBytesAsync(dlg.FileName, bytes);
                Log($"Guardado: {dlg.FileName} ({bytes.Length} bytes)");
            }
            catch (Exception ex)
            {
                Log("Save EXCEPTION: " + ex.Message);
            }
        }
    }
}
