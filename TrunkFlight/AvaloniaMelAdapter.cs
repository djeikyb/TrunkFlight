using System;
using Avalonia.Logging;
using Microsoft.Extensions.Logging;

namespace TrunkFlight;

public class AvaloniaMelAdapter : ILogSink
{
    public bool IsEnabled(LogEventLevel level, string area)
    {
        return TrunkFlight.Log.GetLogger($"Avalonia.Area.{area}").IsEnabled(level switch
        {
            LogEventLevel.Verbose => LogLevel.Trace,
            LogEventLevel.Debug => LogLevel.Debug,
            LogEventLevel.Information => LogLevel.Information,
            LogEventLevel.Warning => LogLevel.Warning,
            LogEventLevel.Error => LogLevel.Error,
            LogEventLevel.Fatal => LogLevel.Critical,
            _ => throw new ArgumentOutOfRangeException(nameof(level), level, null)
        });
    }

    public void Log(LogEventLevel level, string area, object? source, string messageTemplate)
        => Log(level, area, source, messageTemplate, []);

    public void Log(LogEventLevel level, string area, object? source, string messageTemplate, params object?[] args)
    {
        TrunkFlight.Log.GetLogger($"Avalonia.Area.{area}").Log(level switch
        {
            LogEventLevel.Verbose => LogLevel.Trace,
            LogEventLevel.Debug => LogLevel.Debug,
            LogEventLevel.Information => LogLevel.Information,
            LogEventLevel.Warning => LogLevel.Warning,
            LogEventLevel.Error => LogLevel.Error,
            LogEventLevel.Fatal => LogLevel.Critical,
            _ => throw new ArgumentOutOfRangeException(nameof(level), level, null)
        }, messageTemplate, args);
    }
}
