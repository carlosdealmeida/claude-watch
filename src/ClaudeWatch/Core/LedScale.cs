namespace ClaudeWatch.Core;

public sealed record LedSegment(bool Lit, Zone Zone);

public static class LedScale
{
    public const int Count = 15;

    /// <summary>
    /// A cor de cada LED vem da posição. Com <paramref name="warn"/> (o limite acaba antes
    /// do reset), o verde vira âmbar, como o <see cref="Meter.DisplayZone"/> dos outros estilos.
    /// </summary>
    public static IReadOnlyList<LedSegment> Build(int pct, bool warn = false)
    {
        var p = ZoneRules.Clamp(pct);
        var segs = new LedSegment[Count];
        for (var i = 0; i < Count; i++)
        {
            var center = (i + 0.5) * 100.0 / Count;
            var zone = ZoneRules.From((int)Math.Round(center));
            if (warn && zone == Zone.Verde) zone = Zone.Ambar;
            segs[i] = new LedSegment(center <= p, zone);
        }
        return segs;
    }
}
