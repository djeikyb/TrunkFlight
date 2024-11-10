using System.Reactive;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Markup.Xaml;
using Avalonia.Media;
using R3;

namespace TrunkFlight.Views;

public partial class SideBar : UserControl
{
    public SideBar()
    {
        InitializeComponent();

        IBrush? original = null;
        SideBarBackgroundSwitch
            .GetObservable(ToggleButton.IsCheckedProperty)
            .ToObservable()
            .Subscribe(next =>
            {
                if (next ?? false)
                {
                    original = this.Background;
                    this.Background = Brushes.Transparent;
                }
                else
                {
                    this.Background = original;
                }
            });
    }
}
