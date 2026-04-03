using Calcpad.Server;
using Calcpad.Server.Services;

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
    FileLogger.LogInfo("Starting Calcpad Server for Linux");
    
    // Set default Linux environment variables if not set
    Environment.SetEnvironmentVariable("CALCPAD_PORT", Environment.GetEnvironmentVariable("CALCPAD_PORT") ?? "9420");
    Environment.SetEnvironmentVariable("CALCPAD_HOST", Environment.GetEnvironmentVariable("CALCPAD_HOST") ?? "0.0.0.0");
    
    // Create and configure web application using shared service
    var (app, serverUrl) = CalcpadApiService.CreateConfiguredApp(args);

    FileLogger.LogInfo("Starting console application", serverUrl);
    Console.WriteLine($"Calcpad Server starting at {serverUrl}");
    Console.WriteLine("Press Ctrl+C to stop the server.");
    Console.WriteLine($"API Documentation: {serverUrl}/swagger");
    Console.WriteLine($"Sample Client: Open sample-client.html in a browser");
    
    // Setup graceful shutdown
    var cts = new CancellationTokenSource();
    Console.CancelKeyPress += (sender, e) =>
    {
        FileLogger.LogInfo("Received Ctrl+C, shutting down");
        e.Cancel = true;
        cts.Cancel();
    };
    
    var runTask = Task.Run(async () =>
    {
        try
        {
            await app.RunAsync();
        }
        catch (Exception ex)
        {
            FileLogger.LogCrash(ex, "Web application");
        }
    });
    
    try
    {
        await Task.Delay(-1, cts.Token);
    }
    catch (OperationCanceledException)
    {
        Console.WriteLine("Shutting down...");
        await app.StopAsync();
    }
    
    FileLogger.LogInfo("Application shutdown complete");
}
catch (Exception ex)
{
    FileLogger.LogCrash(ex, "Linux console application");
    Console.WriteLine($"ERROR: {ex.Message}");
    Console.WriteLine($"Log file: {FileLogger.GetLogFilePath()}");
    throw;
}