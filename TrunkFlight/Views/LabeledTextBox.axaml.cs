using System.Linq;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;

namespace TrunkFlight.Views;

/// https://github.com/AvaloniaUI/Avalonia/discussions/14340#discussioncomment-8233251
public class SetterBehavior : AvaloniaObject
{
    public static readonly AttachedProperty<ColumnDefinitions> ColumnDefinitionsProperty =
        AvaloniaProperty.RegisterAttached<SetterBehavior, ColumnDefinitions>("ColumnDefinitions",
            typeof(SetterBehavior));

    public static void SetColumnDefinitions(AvaloniaObject element, ColumnDefinitions value) =>
        element.SetValue(ColumnDefinitionsProperty, value);

    public static ColumnDefinitions GetColumnDefinitions(AvaloniaObject element) =>
        element.GetValue(ColumnDefinitionsProperty);

    static SetterBehavior()
    {
        ColumnDefinitionsProperty.Changed.AddClassHandler<Grid, ColumnDefinitions>((grid, e) =>
        {
            grid.ColumnDefinitions.Clear();

            if (e.NewValue.GetValueOrDefault() is ColumnDefinitions columns)
                grid.ColumnDefinitions.AddRange(columns.Select(o => new ColumnDefinition()
                {
                    Width = o.Width,
                    SharedSizeGroup = o.SharedSizeGroup,
                }));
        });
    }
}

public class LabeledTextBox : TemplatedControl
{
    public static readonly StyledProperty<string?> LabelProperty;
    public static readonly StyledProperty<int> LabelWidthProperty;
    public static readonly StyledProperty<string?> TextProperty;

    public static readonly StyledProperty<ColumnDefinitions> ColDefsProperty;

    static LabeledTextBox()
    {
        LabelProperty = AvaloniaProperty.Register<LabeledText, string?>(nameof(Label));
        LabelWidthProperty = AvaloniaProperty.Register<LabeledTextBox, int>(nameof(LabelWidth));

        // LabelWidthProperty.Changed.AddClassHandler<LabeledTextBox>((sender, args) =>
        // {
        //     sender.ColDefs = ColumnDefinitions.Parse($"{args.NewValue} *");
        // });

        ColDefsProperty =
            AvaloniaProperty.Register<LabeledTextBox, ColumnDefinitions>(nameof(ColDefs),
                defaultValue: ColumnDefinitions.Parse("auto *"));
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

    public ColumnDefinitions ColDefs
    {
        get => GetValue(ColDefsProperty);
        set => SetValue(ColDefsProperty, value);
    }
}
