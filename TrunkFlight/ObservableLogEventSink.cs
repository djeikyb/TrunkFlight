using System;
using System.Collections.Generic;
using Avalonia;
using ObservableCollections;
using Serilog;
using Serilog.Configuration;
using Serilog.Core;
using Serilog.Events;
using Serilog.Templates;
using Logger = Avalonia.Logging.Logger;

namespace TrunkFlight;

public static class ObservableLogEventSinkExtensions
{
    public static LoggerConfiguration ObservableSink(
        this LoggerSinkConfiguration loggerConfiguration,
        ObservableLogEventSink logsSink
    ) => loggerConfiguration.Sink(logsSink);

    public static AppBuilder UseSerilog(this AppBuilder builder, ObservableLogEventSink sink)
    {
        Logger.Sink = new AvaloniaSerilogAdapter();
        return builder.AfterSetup((Action<AppBuilder>)(_ =>
        {
            var lc = new LoggerConfiguration();
            lc.Enrich.FromLogContext();
            lc.MinimumLevel.Verbose();

            lc.WriteTo.Conditional(
                le =>
                {
                    var scalar = le.Properties.GetValueOrDefault("source") as ScalarValue;
                    if (scalar?.Value is not string s) return true;
                    if (s.StartsWith("Avalonia.")) return false;
                    if (le.Level < LogEventLevel.Warning) return false;
                    return true;
                },
                lsc => lsc.ObservableSink(sink)
            );


            var template = "[{@t:HH:mm:ss.fff} {@l:u3}] {@m}\n"
                           + "{#each k, v in @p}             \u2570\u2500\u2500 {k} = {v}{#delimit}\n{#end}\n{@x}";

            lc.WriteTo.Console(new ExpressionTemplate(template, theme: MyTemplateThemes.Mine));

            Log.Logger = lc.CreateLogger();
        }));
    }
}

public class ObservableLogEventSink(int capacity) : ILogEventSink
{
    public ObservableFixedSizeRingBuffer<LogEvent> Logs { get; } = new(capacity);
    public void Emit(LogEvent logEvent) => Logs.AddLast(logEvent);
}
