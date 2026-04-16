using Calcpad.Server;
using Calcpad.Server.Services;
using System.Windows.Forms;

// Set up global exception handling
AppDomain.CurrentDomain.UnhandledException += (sender, e) =>
{
    FileLogger.LogCrash((Exception)e.ExceptionObject, "AppDomain.UnhandledException");
};

TaskScheduler.UnobservedTaskException += (sender, e) =>
{
    FileLogger.LogCrash(e.Exception, "TaskScheduler.UnobservedTaskException");
    e.SetObserved();
};

try
{
    FileLogger.LogInfo("Starting Calcpad Server for Windows");
    
    // Set default Windows environment variables if not set
    Environment.SetEnvironmentVariable("CALCPAD_PORT", Environment.GetEnvironmentVariable("CALCPAD_PORT") ?? "9421");
    Environment.SetEnvironmentVariable("CALCPAD_HOST", Environment.GetEnvironmentVariable("CALCPAD_HOST") ?? "localhost");
    
    Application.EnableVisualStyles();
    Application.SetCompatibleTextRenderingDefault(false);
    
    // Set up Windows Forms exception handling
    Application.SetUnhandledExceptionMode(UnhandledExceptionMode.CatchException);
    Application.ThreadException += (sender, e) =>
    {
        FileLogger.LogCrash(e.Exception, "Windows Forms ThreadException");
    };
    
    FileLogger.LogInfo("Creating web server");
    
    // Create web server components using shared service
    var (app, serverUrl) = CalcpadApiService.CreateConfiguredApp(args);
    
    FileLogger.LogInfo("Starting web server in background", serverUrl);
    
    // Start web server in background task
    var serverTask = Task.Run(async () =>
    {
        try
        {
            await app.RunAsync();
        }
        catch (Exception ex)
        {
            FileLogger.LogCrash(ex, "Web server task");
        }
    });
    
    // Give server time to start
    Thread.Sleep(2000);
    
    // Test if server started successfully
    bool serverStarted = false;
    try
    {
        using var httpClient = new HttpClient();
        httpClient.Timeout = TimeSpan.FromSeconds(3);
        var response = httpClient.GetAsync($"{serverUrl}/api/calcpad/sample").Result;
        serverStarted = response.IsSuccessStatusCode;
    }
    catch
    {
        serverStarted = false;
    }
    
    FileLogger.LogInfo("Server startup check", $"Started: {serverStarted}");
    
    // Create and run tray application
    FileLogger.LogInfo("Creating tray application");
    var trayApp = new TrayApplication(app, serverUrl, !serverStarted);
    
    FileLogger.LogInfo("Starting Windows Forms message loop");
    Application.Run(trayApp);
    
    FileLogger.LogInfo("Windows application ended");
}
catch (Exception ex)
{
    FileLogger.LogCrash(ex, "Windows application");
    MessageBox.Show($"Application error: {ex.Message}\n\nCheck log file for details.", 
                   "Calcpad Server Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
}