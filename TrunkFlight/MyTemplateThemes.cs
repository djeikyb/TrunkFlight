using System.Collections.Generic;
using Serilog.Templates.Themes;

namespace TrunkFlight;

public class MyTemplateThemes
{
    /// <remarks>
    /// <para>
    /// Color is two args.. eg bold yellow foreground is 93; basic yellow background is 43.
    /// Least significant digit is a colour, the rest determines fg, bg, basic, and bright.
    /// </para>
    /// <list type="table">
    ///   <listheader><term>msb</term><description>sets</description></listheader>
    ///   <item><term>3</term><description>basic foreground</description></item>
    ///   <item><term>9</term><description>bright foreground</description></item>
    ///   <item><term>4</term><description>basic background</description></item>
    ///   <item><term>10</term><description>bright background</description></item>
    /// </list>
    /// <list type="table">
    ///   <listheader><term>lsb</term><description>sets color</description></listheader>
    ///   <item><term>0</term><description>black</description></item>
    ///   <item><term>1</term><description>red</description></item>
    ///   <item><term>2</term><description>green</description></item>
    ///   <item><term>3</term><description>yellow</description></item>
    ///   <item><term>4</term><description>blue</description></item>
    ///   <item><term>5</term><description>magenta</description></item>
    ///   <item><term>6</term><description>cyan</description></item>
    ///   <item><term>7</term><description>white</description></item>
    /// </list>
    /// </remarks>
    public static TemplateTheme Mine { get; } = new(
        new Dictionary<TemplateThemeStyle, string>
        {
            [TemplateThemeStyle.Text] = "\u001B[34;1m",
            [TemplateThemeStyle.SecondaryText] = "\u001B[30m",
            [TemplateThemeStyle.TertiaryText] = "\u001B[35;1m",
            [TemplateThemeStyle.Invalid] = "\u001B[35;1m\u001B[47m",
            [TemplateThemeStyle.Null] = "\u001B[1m\u001B[37;1m",
            [TemplateThemeStyle.Name] = "\u001B[36m",
            [TemplateThemeStyle.String] = "\u001B[1m\u001B[37;1m",
            [TemplateThemeStyle.Number] = "\u001B[1m\u001B[37;1m",
            [TemplateThemeStyle.Boolean] = "\u001B[1m\u001B[37;1m",
            [TemplateThemeStyle.Scalar] = "\u001B[1m\u001B[32;1m",
            [TemplateThemeStyle.LevelVerbose] = "\u001B[37;3m",
            [TemplateThemeStyle.LevelDebug] = "\u001B[37m",
            [TemplateThemeStyle.LevelInformation] = "\u001B[37;1m",
            [TemplateThemeStyle.LevelWarning] = "\u001B[35;1;3m",
            [TemplateThemeStyle.LevelError] = "\u001B[31;1m",
            [TemplateThemeStyle.LevelFatal] = "\u001B[30;46m"
        });
}
