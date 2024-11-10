using System;
using Avalonia;
using Merviche.Logging.Serilog;
using Serilog;

namespace TrunkFlight;

class Program
{
    // Initialization code. Don't use any Avalonia, third-party APIs or any
    // SynchronizationContext-reliant code before AppMain is called: things aren't initialized
    // yet and stuff might break.
    [STAThread]
    public static void Main(string[] args) => BuildAvaloniaApp()
        .StartWithClassicDesktopLifetime(args);

    // Avalonia configuration, don't remove; also used by visual designer.
    public static AppBuilder BuildAvaloniaApp()
        => AppBuilder.Configure<App>()
            .UsePlatformDetect()
            .WithInterFont()
            .UseR3()
            .UseSerilog(App.LogsSink)
            .AfterSetup(_ =>
            {
                var logger = Log.ForContext<Program>();
                var scope = logger.ForContext("foo", "bar");
                scope.Information("App setup complete!");
                scope.Verbose("App setup complete!");
                scope.Debug("App setup complete!");
                scope.Information("App setup complete!");
                scope.Warning("App setup complete!");
                scope.Error("App setup complete!");
            });
}
