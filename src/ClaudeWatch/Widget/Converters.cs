using System.Globalization;
using System.Windows;
using System.Windows.Data;
using System.Windows.Media;
using ClaudeWatch.Core;

namespace ClaudeWatch.Widget;

public sealed class PctToBrushConverter : IValueConverter
{
    public object Convert(object v, Type t, object p, CultureInfo c) =>
        (SolidColorBrush)new BrushConverter().ConvertFromString(
            ZoneColors.Hex(ZoneRules.From(System.Convert.ToInt32(v))))!;
    public object ConvertBack(object v, Type t, object p, CultureInfo c) => throw new NotSupportedException();
}

public sealed class ZoneToBrushConverter : IValueConverter
{
    public object Convert(object v, Type t, object p, CultureInfo c) =>
        ZoneBrushes.Of(v is Zone z ? z : Zone.Verde);
    public object ConvertBack(object v, Type t, object p, CultureInfo c) => throw new NotSupportedException();
}

/// <summary>
/// Percentual → largura proporcional de coluna do Grid: <c>42</c> vira <c>42*</c>,
/// e com ConverterParameter=rest, <c>58*</c>. Nulo vira 0*.
/// </summary>
public sealed class PctToStarConverter : IValueConverter
{
    public object Convert(object v, Type t, object p, CultureInfo c)
    {
        var pct = v is null ? 0 : Math.Clamp(System.Convert.ToDouble(v), 0, 100);
        return new GridLength(p as string == "rest" ? 100 - pct : pct, GridUnitType.Star);
    }
    public object ConvertBack(object v, Type t, object p, CultureInfo c) => throw new NotSupportedException();
}

public sealed class LedSegmentsConverter : IValueConverter
{
    public object Convert(object v, Type t, object p, CultureInfo c) =>
        LedScale.Build(System.Convert.ToInt32(v));
    public object ConvertBack(object v, Type t, object p, CultureInfo c) => throw new NotSupportedException();
}

public sealed class LocalHhmmConverter : IValueConverter
{
    public object Convert(object v, Type t, object p, CultureInfo c) =>
        v is DateTimeOffset d ? d.ToLocalTime().ToString("HH:mm", c) : "";
    public object ConvertBack(object v, Type t, object p, CultureInfo c) => throw new NotSupportedException();
}

public sealed class NullToCollapsedConverter : IValueConverter
{
    public object Convert(object v, Type t, object p, CultureInfo c) =>
        v is null ? Visibility.Collapsed : Visibility.Visible;
    public object ConvertBack(object v, Type t, object p, CultureInfo c) => throw new NotSupportedException();
}
