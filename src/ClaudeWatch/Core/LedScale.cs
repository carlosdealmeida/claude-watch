namespace ClaudeWatch.Core;

public sealed record LedSegment(bool Lit, Zone Zone);

public static class LedScale
{
    public const int Count = 15;

    public static IReadOnlyList<LedSegment> Build(int pct)
    {
        var p = ZoneRules.Clamp(pct);
        var segs = new LedSegment[Count];
        for (var i = 0; i < Count; i++)
        {
            var center = (i + 0.5) * 100.0 / Count;
            segs[i] = new LedSegment(center <= p, ZoneRules.From((int)Math.Round(center)));
        }
        return segs;
    }
}
