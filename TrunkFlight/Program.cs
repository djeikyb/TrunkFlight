using System;
using Avalonia;
using Merviche.Logging;
using Microsoft.Extensions.Logging;

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
            .UseZLogger(App.LogsProvider)
            .AfterSetup(_ =>
            {
                var logger = Log.Logger;
                var scope = logger.With("foo", "bar");
                scope.LogTrace("App setup complete!");
                scope.LogDebug("App setup complete!");
                scope.LogInformation("App setup complete!");
                scope.LogWarning("App setup complete!");
                scope.LogError("App setup complete!");

                try
                {
                    throw new Exception("oh noes");
                }
                catch (Exception e)
                {
                    logger.LogError(e, "caught!");
                }
            });
}
