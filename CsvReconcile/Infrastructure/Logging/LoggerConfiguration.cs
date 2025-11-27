using Serilog;
using Serilog.Events;

namespace CsvReconcile.Infrastructure.Logging;

/// <summary>
/// Configures Serilog logger for the application
/// </summary>
public static class LoggerConfiguration
{
    public static ILogger CreateLogger(
        string? outputFolder = null,
        LogEventLevel minimumLevel = LogEventLevel.Information,
        bool consoleLogging = true)
    {
        var logFolder = outputFolder ?? "Logs";
        
        if (!Directory.Exists(logFolder))
        {
            Directory.CreateDirectory(logFolder);
        }

        var logFile = Path.Combine(logFolder, $"reconciliation-{DateTime.Now:yyyyMMdd-HHmmss}.log");

        var loggerConfig = new Serilog.LoggerConfiguration()
            .MinimumLevel.Is(minimumLevel)
            .Enrich.FromLogContext()
            .Enrich.WithThreadId();

        // Add console sink only if enabled
        if (consoleLogging)
        {
            loggerConfig.WriteTo.Console(
                outputTemplate: "[{Timestamp:HH:mm:ss} {Level:u3}] {Message:lj} (Thread: {ThreadId}){NewLine}{Exception}");
        }

        // Always write to file
        loggerConfig.WriteTo.File(
            logFile,
            outputTemplate: "[{Timestamp:yyyy-MM-dd HH:mm:ss.fff zzz} {Level:u3}] {Message:lj} (Thread: {ThreadId}){NewLine}{Exception}",
            rollingInterval: RollingInterval.Day,
            retainedFileCountLimit: 7);

        return loggerConfig.CreateLogger();
    }
}

