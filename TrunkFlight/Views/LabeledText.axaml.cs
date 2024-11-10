using Avalonia;
using Avalonia.Controls.Primitives;

namespace TrunkFlight.Views;

public class LabeledText : TemplatedControl
{
    public static readonly StyledProperty<string?> LabelProperty;
    public static readonly StyledProperty<int> LabelWidthProperty;
    public static readonly StyledProperty<string?> TextProperty;

    static LabeledText()
    {
        LabelProperty = AvaloniaProperty.Register<LabeledText, string?>(nameof(Label));
        LabelWidthProperty = AvaloniaProperty.Register<LabeledTextBox, int>(nameof(LabelWidth));
        TextProperty = AvaloniaProperty.Register<LabeledText, string?>(nameof(Text));
    }

    public string? Label
    {
        get => GetValue(LabelProperty);
        set => SetValue(LabelProperty, value);
    }

    public string? Text
    {
        get => GetValue(TextProperty);
        set => SetValue(TextProperty, value);
    }

    public int LabelWidth
    {
        get => GetValue(LabelWidthProperty);
        set => SetValue(LabelWidthProperty, value);
    }
}
