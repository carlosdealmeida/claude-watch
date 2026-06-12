using System.Globalization;
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

public sealed class PctToRingGeometryConverter : IValueConverter
{
    // anel 56px, stroke 6 → r=25, centro (28,28), início no topo, sentido horário
    public object Convert(object v, Type t, object p, CultureInfo c)
    {
        var pct = Math.Clamp(System.Convert.ToDouble(v), 0, 100);
        var ang = Math.Min(pct / 100.0 * 360.0, 359.9) * Math.PI / 180.0;
        const double r = 25, cx = 28, cy = 28;
        var fig = new PathFigure { StartPoint = new(cx, cy - r), IsClosed = false };
        fig.Segments.Add(new ArcSegment(
            new(cx + r * Math.Sin(ang), cy - r * Math.Cos(ang)),
            new(r, r), 0, pct > 50, SweepDirection.Clockwise, true));
        var geo = new PathGeometry { Figures = { fig } };
        geo.Freeze();
        return geo;
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
