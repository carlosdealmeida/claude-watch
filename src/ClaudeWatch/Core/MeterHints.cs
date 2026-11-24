using System.Globalization;

namespace ClaudeWatch.Core;

/// <summary>
/// Ritmo de consumo e a linha de texto sob cada medidor.
///
/// O ritmo não precisa de histórico: a resposta traz o início e o fim da janela,
/// então "usei X% em T horas" projeta quando chega a 100% se o ritmo continuar.
/// No Ultra, com só limites semanais, essa é a pergunta que importa:
/// "chego até o reset?".
/// </summary>
public static class MeterHints
{
    private static readonly CultureInfo PtBr = CultureInfo.GetCultureInfo("pt-BR");

    /// <summary>Abaixo disto a projeção é ruído (≈7h numa semana, 12 min numa sessão de 5h).</summary>
    private const double MinElapsedFraction = 0.04;
    private const int MinPct = 2;

    public static Meter Annotate(Meter m, DateTimeOffset now, TimeZoneInfo tz)
    {
        var runsOut = RunsOutAt(m, now);
        var hint =
            m.Exhausted ? (m.ResetAt is { } r1 ? $"esgotado · volta {Until(r1, now, tz)}" : "esgotado")
            // A projeção fica no horário: "em 3h" daria a ela uma precisão que não tem.
            : runsOut is { } eta ? $"⚠ acaba {When(eta, now, tz)}"
            : m.ResetAt is { } r2 ? $"↻ {Until(r2, now, tz)}"
            : null;
        return m with { RunsOutAt = runsOut, Hint = hint };
    }

    /// <summary>
    /// Recalcula as linhas com a hora de agora, para a contagem andar entre uma leitura e
    /// outra (e quando a leitura falha e o último dado é reaproveitado).
    /// </summary>
    public static UsageSnapshot Refresh(UsageSnapshot s, DateTimeOffset now, TimeZoneInfo tz) =>
        s with { Meters = s.Meters.Select(m => Annotate(m, now, tz)).ToList() };

    /// <summary>
    /// "em 2h13" quando falta menos de 24h; mais longe que isso (ou já passado),
    /// o horário de <see cref="When"/>. Arredonda para cima: nunca diz que volta antes da hora.
    /// </summary>
    public static string Until(DateTimeOffset t, DateTimeOffset now, TimeZoneInfo tz)
    {
        var left = t - now;
        if (left <= TimeSpan.Zero || left >= TimeSpan.FromHours(24)) return When(t, now, tz);
        var mins = (int)Math.Ceiling(left.TotalMinutes);
        return mins < 60 ? $"em {mins}min"
            : mins % 60 == 0 ? $"em {mins / 60}h"
            : $"em {mins / 60}h{mins % 60:00}";
    }

    /// <summary>
    /// Quando o limite chega a 100% no ritmo atual, se isso acontecer antes do reset.
    /// Nulo quando o ritmo dá até o reset, quando já esgotou, ou quando ainda é cedo para projetar.
    /// </summary>
    public static DateTimeOffset? RunsOutAt(Meter m, DateTimeOffset now)
    {
        if (m.Exhausted || m.Pct < MinPct) return null;
        if (m.ResetAt is not { } reset || m.WindowStart is not { } start) return null;

        var window = reset - start;
        var elapsed = now - start;
        if (window <= TimeSpan.Zero || elapsed <= TimeSpan.Zero || now >= reset) return null;
        if (elapsed.TotalSeconds < window.TotalSeconds * MinElapsedFraction) return null;

        var eta = now + TimeSpan.FromSeconds(elapsed.TotalSeconds * (100.0 - m.Pct) / m.Pct);
        return eta < reset ? eta : null;
    }

    /// <summary>"14:30" se for hoje, "sáb 01:07" se for nesta semana, "02/12 09:00" se depois.</summary>
    public static string When(DateTimeOffset t, DateTimeOffset now, TimeZoneInfo tz)
    {
        var local = TimeZoneInfo.ConvertTime(t, tz);
        var today = TimeZoneInfo.ConvertTime(now, tz).Date;
        var days = (local.Date - today).TotalDays;
        var fmt = days == 0 ? "HH:mm" : days is > 0 and < 7 ? "ddd HH:mm" : "dd/MM HH:mm";
        return local.ToString(fmt, PtBr).Replace(".", "");
    }
}
