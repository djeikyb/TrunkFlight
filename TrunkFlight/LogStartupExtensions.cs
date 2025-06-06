using System;
using System.Runtime.InteropServices;
using Avalonia;
using Avalonia.Logging;
using Microsoft.Extensions.Logging;
using ZLogger;

namespace TrunkFlight;

public static class LogStartupExtensions
{
    /// <summary>
    /// Uses Microsoft.Extensions.Logging, backed by Cysharp's ZLogger.
    /// </summary>
    /// <param name="builder"></param>
    /// <param name="guiProvider">ViewModel that stores logs.</param>
    public static AppBuilder UseZLogger(
        this AppBuilder builder,
        ObservableLogProvider? guiProvider = null
    )
    {
        // Send internal Avalonia logs to our Microsoft.Extensions.Logging provider.
        // With types expanded, it reads:
        // Avalonia.Logging.Logger.Sink = new AvaloniaApplication1.AvaloniaMelAdapter()
        Logger.Sink = new AvaloniaMelAdapter();

        // Configure Microsoft.Extensions.Logging
        var factory = LoggerFactory.Create(logging =>
        {
            // Layout logging can be verbose. Fine if going to a file. But can
            // crash a data grid. Every log added to the data grid causes a new
            // layout log!
            logging.AddFilter($"Avalonia.Area.Layout", LogLevel.Warning);

            logging.SetMinimumLevel(LogLevel.Debug);

            logging.AddZLoggerConsole(o =>
            {
                // o.IncludeScopes = true;
                o.UsePlainTextFormatter(formatter =>
                {
                    formatter.SetPrefixFormatter($"{0:timeonly} {1}] ", (in MessageTemplate template, in LogInfo info) =>
                        template.Format(info.Timestamp, info.LogLevel switch
                        {
                            LogLevel.Trace => "\e[0m\e[3mTRA\e[0m",
                            LogLevel.Debug => "\e[0;90mDBG\e[0m",
                            LogLevel.Information => "INF",
                            LogLevel.Warning => "\e[0;33mWRN\e[0m",
                            LogLevel.Error => "\e[0;91mERR\e[0m",
                            LogLevel.Critical => "CRI",
                            LogLevel.None => "NON",
                            _ => "🥑"
                        }));
                });
            });

            if (guiProvider is not null) logging.AddZLoggerLogProcessor(guiProvider);
        });

        // Initialize global logger and logger factory
        Log.SetLoggerFactory(factory, "App");

        if (OperatingSystem.IsWindows())
        {
            AttachConsole(ATTACH_PARENT_PROCESS);
            Console.WriteLine("🥑 did it work");
        }

        return builder;
    }

    [DllImport("kernel32.dll")]
    static extern bool AttachConsole(uint dwProcessId);

    const uint ATTACH_PARENT_PROCESS = 0x0ffffffff;
}
