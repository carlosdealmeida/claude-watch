namespace ClaudeWatch.Core;

public static class TooltipFormatter
{
    public static string Format(UsageSnapshot s, bool local = true)
    {
        var reset = s.FiveHour.ResetAt is { } r
            ? $" (reset {(local ? r.ToLocalTime() : r):HH:mm})" : "";
        var text = $"5h {s.FiveHour.Pct}%{reset} · Sem {s.Week.Pct}% · Opus {s.Opus.Pct}%";
        return text.Length <= 127 ? text : text[..127];
    }
}
