namespace ClaudeWatch.Core;

/// <summary>
/// Tooltip do ícone do tray: "Sem 12% (reset dom 09:00) · Fable 18% (acaba sáb 01:07)".
/// Cada medidor em alerta diz quando acaba; o primeiro, se nenhum estiver em alerta,
/// diz quando reseta. O Windows corta o tooltip em 127 caracteres.
/// </summary>
public static class TooltipFormatter
{
    public static string Format(UsageSnapshot s, TimeZoneInfo? tz = null)
    {
        var zone = tz ?? TimeZoneInfo.Local;
        var now = s.CollectedAt;
        var anyWarn = s.Meters.Any(m => m.Warn);

        var parts = s.Meters.Select((m, i) =>
        {
            var extra =
                m.Exhausted ? " (esgotado)"
                : m.RunsOutAt is { } eta ? $" (acaba {MeterHints.When(eta, now, zone)})"
                : i == 0 && !anyWarn && m.ResetAt is { } r ? $" (reset {MeterHints.When(r, now, zone)})"
                : "";
            return $"{m.Short} {m.Pct}%{extra}";
        });

        var text = string.Join(" · ", parts);
        if (text.Length == 0) text = "ClaudeWatch";
        return text.Length <= 127 ? text : text[..127];
    }
}
