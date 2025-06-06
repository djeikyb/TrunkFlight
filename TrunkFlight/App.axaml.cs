using System;
using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using HotAvalonia;
using TrunkFlight.Vm;

namespace TrunkFlight;

public class App : Application
{
    static App()
    {
        LogsProvider = new ObservableLogProvider(14);
    }

    public static ObservableLogProvider LogsProvider { get; }

    public override void Initialize()
    {
        AvaloniaXamlLoader.Load(this);
    }

    public override void OnFrameworkInitializationCompleted()
    {
        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            var mw = new MainWindow();
            mw.DataContext = new MainViewModel(mw.Clipboard ?? throw new Exception("Missing clipboard."));
            desktop.MainWindow = mw;
        }

        base.OnFrameworkInitializationCompleted();
    }
}
