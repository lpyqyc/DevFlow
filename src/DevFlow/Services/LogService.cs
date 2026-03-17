using System;
using Serilog;

namespace DevFlow.Services;

public interface ILogService
{
    void Debug(string messageTemplate, params object?[] propertyValues);
    void Information(string messageTemplate, params object?[] propertyValues);
    void Warning(string messageTemplate, params object?[] propertyValues);
    void Error(string messageTemplate, params object?[] propertyValues);
    void Error(Exception exception, string messageTemplate, params object?[] propertyValues);
    void Close();
}

public class LogService : ILogService
{
    public static ILogService Instance { get; private set; } = null!;
    
    private readonly ILogger _logger;

    public LogService()
    {
        var appDir = AppContext.BaseDirectory;
        var logDir = System.IO.Path.Combine(appDir, "logs");
        
        if (!System.IO.Directory.Exists(logDir))
        {
            System.IO.Directory.CreateDirectory(logDir);
        }

        var logPath = System.IO.Path.Combine(logDir, "devflow-.log");

        _logger = new LoggerConfiguration()
            .MinimumLevel.Debug()
            .WriteTo.Console(outputTemplate: 
                "[{Timestamp:HH:mm:ss.fff}] [{Level:u3}] [{SourceContext}] {Message:lj}{NewLine}{Exception}")
            .WriteTo.File(logPath, 
                rollingInterval: RollingInterval.Day,
                outputTemplate: 
                "{Timestamp:yyyy-MM-dd HH:mm:ss.fff} [{Level:u3}] [{SourceContext}] {Message:lj}{NewLine}{Exception}")
            .Enrich.FromLogContext()
            .CreateLogger();

        Instance = this;
        
        _logger.Information("日志服务初始化完成，日志路径: {LogPath}", logPath);
    }

    public void Debug(string messageTemplate, params object?[] propertyValues)
    {
        _logger.Debug(messageTemplate, propertyValues);
    }

    public void Information(string messageTemplate, params object?[] propertyValues)
    {
        _logger.Information(messageTemplate, propertyValues);
    }

    public void Warning(string messageTemplate, params object?[] propertyValues)
    {
        _logger.Warning(messageTemplate, propertyValues);
    }

    public void Error(string messageTemplate, params object?[] propertyValues)
    {
        _logger.Error(messageTemplate, propertyValues);
    }

    public void Error(Exception exception, string messageTemplate, params object?[] propertyValues)
    {
        _logger.Error(exception, messageTemplate, propertyValues);
    }

    public void Close()
    {
        _logger.Information("日志服务关闭");
        Log.CloseAndFlush();
    }
}

public static class LogHelper
{
    public static void LogDebug(string sourceContext, string message, params object?[] args)
    {
        LogService.Instance?.Debug($"[{sourceContext}] {message}", args);
    }

    public static void LogInfo(string sourceContext, string message, params object?[] args)
    {
        LogService.Instance?.Information($"[{sourceContext}] {message}", args);
    }

    public static void LogWarn(string sourceContext, string message, params object?[] args)
    {
        LogService.Instance?.Warning($"[{sourceContext}] {message}", args);
    }

    public static void LogError(string sourceContext, string message, params object?[] args)
    {
        LogService.Instance?.Error($"[{sourceContext}] {message}", args);
    }
    
    public static void LogWarning(string sourceContext, string message, params object?[] args)
    {
        LogService.Instance?.Warning($"[{sourceContext}] {message}", args);
    }
}
