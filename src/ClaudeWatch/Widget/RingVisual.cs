using System.Windows;
using System.Windows.Media;
using ClaudeWatch.Core;

namespace ClaudeWatch.Widget;

/// <summary>Um anel do skin Anéis já pronto para desenhar: trilho, arco, espessura e cor.</summary>
public sealed record RingVisual(Geometry Track, Geometry Arc, double Thickness, Brush Brush)
{
    public static IReadOnlyList<RingVisual> Build(IReadOnlyList<Meter> meters)
    {
        var n = Math.Min(meters.Count, RingLayout.Max);
        var t = RingLayout.Thickness(n);
        return meters.Take(n).Select((m, i) =>
        {
            var r = RingLayout.Radius(i, n);
            return new RingVisual(Circle(r), RingGeometry.Arc(m.Pct, RingLayout.Center, RingLayout.Center, r), t,
                ZoneBrushes.Of(m.DisplayZone));
        }).ToList();
    }

    private static Geometry Circle(double r)
    {
        var g = new EllipseGeometry(new Point(RingLayout.Center, RingLayout.Center), r, r);
        g.Freeze();
        return g;
    }
}

public static class RingGeometry
{
    /// <summary>Arco do topo, no sentido horário, cobrindo <paramref name="pct"/>% do círculo.</summary>
    public static Geometry Arc(double pct, double cx, double cy, double r)
    {
        pct = Math.Clamp(pct, 0, 100);
        if (pct <= 0) return Geometry.Empty;
        var ang = Math.Min(pct / 100.0 * 360.0, 359.9) * Math.PI / 180.0;
        var fig = new PathFigure { StartPoint = new(cx, cy - r), IsClosed = false };
        fig.Segments.Add(new ArcSegment(
            new(cx + r * Math.Sin(ang), cy - r * Math.Cos(ang)),
            new(r, r), 0, pct > 50, SweepDirection.Clockwise, true));
        var geo = new PathGeometry { Figures = { fig } };
        geo.Freeze();
        return geo;
    }
}

public static class ZoneBrushes
{
    private static readonly Dictionary<Zone, SolidColorBrush> Cache =
        Enum.GetValues<Zone>().ToDictionary(z => z, z =>
        {
            var b = (SolidColorBrush)new BrushConverter().ConvertFromString(ZoneColors.Hex(z))!;
            b.Freeze();
            return b;
        });

    public static SolidColorBrush Of(Zone z) => Cache[z];
}
