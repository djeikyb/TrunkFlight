using System;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using ObservableCollections;
using ZLogger;

namespace TrunkFlight;

// This is our own global logger manager
public static class Log
{
    private static ILogger _globalLogger = default!;
    private static ILoggerFactory _loggerFactory = default!;

    public static void SetLoggerFactory(ILoggerFactory loggerFactory, string categoryName)
    {
        _loggerFactory = loggerFactory;
        _globalLogger = loggerFactory.CreateLogger(categoryName);
    }

    public static ILogger Logger => _globalLogger;

    // standard LoggerFactory caches logger per category so no need to cache in this manager
    public static ILogger<T> GetLogger<T>() where T : class => _loggerFactory.CreateLogger<T>();
    public static ILogger GetLogger(string categoryName) => _loggerFactory.CreateLogger(categoryName);
}

public class ObservableLogProvider(int capacity) : IAsyncLogProcessor
{
    // cf - https://learn.microsoft.com/en-us/dotnet/core/extensions/logging-providers
    //    - https://github.com/Cysharp/ZLogger?tab=readme-ov-file#logging-providers

    public ObservableFixedSizeRingBuffer<LogEvent> Logs { get; } = new(capacity);

    public void Post(IZLoggerEntry entry)
    {
        var ex = entry.LogInfo.Exception;
        Logs.AddLast(new LogEvent
        {
            Level = entry.LogInfo.LogLevel,
            Timestamp = entry.LogInfo.Timestamp.Utc,
            Message = entry.ToString() + (ex is null ? string.Empty : $" {ex}"),
        });
        entry.Return();
    }

    public ValueTask DisposeAsync() => default;
}

public class LogEvent // HRM should this be a struct?
{
    public LogLevel Level { get; init; }
    public DateTimeOffset Timestamp { get; init; }
    public string Message { get; init; }
}
