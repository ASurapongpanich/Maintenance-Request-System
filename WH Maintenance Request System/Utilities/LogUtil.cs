using Microsoft.Extensions.Logging;
using System;
using System.IO;

public static class LogUtil
{
    private static ILoggerFactory _loggerFactory;
    private static readonly string logFilePath = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", "logs", "log.txt");

    public static void Configure(ILoggerFactory loggerFactory)
    {
        _loggerFactory = loggerFactory;

        string logDirectory = Path.GetDirectoryName(logFilePath);
        if (!Directory.Exists(logDirectory))
        {
            Directory.CreateDirectory(logDirectory);
        }
    }

    public static void ErrorLog(string message)
    {
        var logger = _loggerFactory.CreateLogger("ErrorLogger");
        logger.LogError(message);
        WriteToFile("Error", message);
    }

    public static void InfoLog(string message)
    {
        var logger = _loggerFactory.CreateLogger("InfoLogger");
        logger.LogInformation(message);
        WriteToFile("Info", message);
    }

    public static void WarnLog(string message)
    {
        var logger = _loggerFactory.CreateLogger("WarnLogger");
        logger.LogWarning(message);
        WriteToFile("Warning", message);
    }

    private static void WriteToFile(string logType, string message)
    {
        try
        {
            string fullMessage = $"{DateTime.Now:yyyy-MM-dd HH:mm:ss} [{logType}] {message}{Environment.NewLine}";

            File.AppendAllText(logFilePath, fullMessage);
        }
        catch (Exception ex)
        {
            var logger = _loggerFactory.CreateLogger("FileLoggerError");
            logger.LogError($"Failed to write log file: {ex.Message}");
        }
    }
}
