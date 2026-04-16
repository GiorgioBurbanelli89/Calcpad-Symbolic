using System.Reflection;
using System.Text;

namespace Calcpad.Server
{
    public static class FileLogger
    {
        private static readonly object _lock = new object();
        private static string? _logFilePath;
        
        static FileLogger()
        {
            try
            {
                // Get the directory where the executable is located
                var executablePath = Assembly.GetExecutingAssembly().Location;
                if (string.IsNullOrEmpty(executablePath))
                {
                    // Fallback for single-file deployment
                    executablePath = Environment.ProcessPath ?? AppContext.BaseDirectory;
                }
                
                var directory = Path.GetDirectoryName(executablePath) ?? AppContext.BaseDirectory;
                var timestamp = DateTime.Now.ToString("yyyyMMdd");
                _logFilePath = Path.Combine(directory, $"CalcpadServer-{timestamp}.log");
                
                // Write initial log entry
                WriteLog("INFO", "Logger initialized", $"Log file: {_logFilePath}");
            }
            catch (Exception ex)
            {
                // If we can't initialize logging, try a fallback location
                try
                {
                    _logFilePath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Desktop), 
                        $"CalcpadServer-{DateTime.Now:yyyyMMdd}.log");
                    WriteLog("WARN", "Logger fallback location", $"Using desktop: {_logFilePath}, Error: {ex.Message}");
                }
                catch
                {
                    // If all else fails, we'll just skip logging
                    _logFilePath = null;
                }
            }
        }
        
        public static void LogInfo(string message, string? details = null)
        {
            WriteLog("INFO", message, details);
        }
        
        public static void LogWarning(string message, string? details = null)
        {
            WriteLog("WARN", message, details);
        }
        
        public static void LogError(string message, Exception? exception = null)
        {
            var details = exception != null ? 
                $"Exception: {exception.GetType().Name}\nMessage: {exception.Message}\nStackTrace: {exception.StackTrace}" : 
                null;
            WriteLog("ERROR", message, details);
        }
        
        public static void LogCrash(Exception exception, string context = "Application")
        {
            var sb = new StringBuilder();
            sb.AppendLine($"=== CRASH REPORT ===");
            sb.AppendLine($"Context: {context}");
            sb.AppendLine($"Exception Type: {exception.GetType().FullName}");
            sb.AppendLine($"Message: {exception.Message}");
            sb.AppendLine($"Stack Trace:");
            sb.AppendLine(exception.StackTrace);
            
            // Include inner exceptions
            var innerEx = exception.InnerException;
            var level = 1;
            while (innerEx != null)
            {
                sb.AppendLine($"--- Inner Exception {level} ---");
                sb.AppendLine($"Type: {innerEx.GetType().FullName}");
                sb.AppendLine($"Message: {innerEx.Message}");
                sb.AppendLine($"Stack Trace:");
                sb.AppendLine(innerEx.StackTrace);
                innerEx = innerEx.InnerException;
                level++;
            }
            
            sb.AppendLine($"=== END CRASH REPORT ===");
            
            WriteLog("CRASH", "Application crashed", sb.ToString());
            
            // Also write to console if available
            try
            {
                Console.WriteLine("CRASH: " + exception.Message);
                Console.WriteLine("Details written to: " + _logFilePath);
            }
            catch { /* Ignore console errors */ }
        }
        
        private static void WriteLog(string level, string message, string? details = null)
        {
            if (string.IsNullOrEmpty(_logFilePath))
                return;
                
            try
            {
                lock (_lock)
                {
                    var timestamp = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss.fff");
                    var logEntry = $"[{timestamp}] [{level}] {message}";
                    
                    if (!string.IsNullOrEmpty(details))
                    {
                        logEntry += Environment.NewLine + details;
                    }
                    
                    logEntry += Environment.NewLine;
                    
                    File.AppendAllText(_logFilePath, logEntry, Encoding.UTF8);
                }
            }
            catch
            {
                // If logging fails, we can't do much about it
                // Don't throw exceptions from the logger
            }
        }
        
        public static string? GetLogFilePath() => _logFilePath;
    }
}