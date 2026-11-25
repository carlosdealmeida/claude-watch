namespace ClaudeWatch.Core;

/// <summary>
/// Decide quando avisar, sem a pessoa olhar o widget: quando um medidor passa de 70% ou de 90%,
/// esgota, ou o ritmo diz que acaba antes do reset (o "⚠ acaba", que no Ultra aparece muito
/// antes dos 70%). Cada aviso sai uma vez por janela do medidor e volta a valer no reset
/// (a projeção oscila quando a pessoa pausa, e não pode virar um aviso a cada leitura).
///
/// O que já vale na primeira leitura de um medidor não é avisado: o widget mostra, e um aviso
/// a cada login seria ruído. Dado velho (leitura que falhou) não avisa nem conta como visto.
/// </summary>
public sealed class UsageAlerts
{
    private enum Level { Ritmo, Ambar, Vermelho, Esgotado }

    /// <summary>Até 1h de diferença no reset é o mesmo reset (a API arredonda de leitura em leitura).</summary>
    private static readonly TimeSpan SameReset = TimeSpan.FromHours(1);

    private sealed record Seen(DateTimeOffset? Reset, HashSet<Level> Fired);
    private readonly Dictionary<string, Seen> _seen = [];

    public IReadOnlyList<string> Check(UsageSnapshot s, DateTimeOffset now, TimeZoneInfo tz)
    {
        if (s.State != SnapshotState.Ok) return [];

        var messages = new List<string>();
        foreach (var m in s.Meters)
        {
            var key = m.Key.Length > 0 ? m.Key : m.Label;
            var active = Active(m);

            if (!_seen.TryGetValue(key, out var seen))
            {
                _seen[key] = new Seen(m.ResetAt, [.. active]);
                continue;
            }
            if (!SameWindow(seen.Reset, m.ResetAt))
                _seen[key] = seen = new Seen(m.ResetAt, []);

            var fresh = active.Where(l => !seen.Fired.Contains(l)).ToList();
            seen.Fired.UnionWith(active);
            if (fresh.Count > 0) messages.Add(Message(m, fresh.Max(), now, tz));
        }
        return messages;
    }

    private static List<Level> Active(Meter m)
    {
        var l = new List<Level>();
        if (m.RunsOutAt is not null) l.Add(Level.Ritmo);
        if (m.Pct >= 70) l.Add(Level.Ambar);
        if (m.Pct >= 90) l.Add(Level.Vermelho);
        if (m.Exhausted) l.Add(Level.Esgotado);
        return l;
    }

    private static bool SameWindow(DateTimeOffset? a, DateTimeOffset? b) =>
        (a, b) switch
        {
            (null, null) => true,
            ({ } x, { } y) => (x - y).Duration() <= SameReset,
            _ => false,
        };

    private static string Message(Meter m, Level top, DateTimeOffset now, TimeZoneInfo tz) => top switch
    {
        Level.Esgotado => m.ResetAt is { } r
            ? $"{m.Label} esgotou · volta {MeterHints.Until(r, now, tz)}"
            : $"{m.Label} esgotou",
        Level.Ritmo => $"{m.Label} em {m.Pct}%: no ritmo atual acaba {MeterHints.When(m.RunsOutAt!.Value, now, tz)}, antes do reset",
        _ => $"{m.Label} passou de {(top == Level.Vermelho ? 90 : 70)}% ({m.Pct}%)"
             + (m.RunsOutAt is { } eta ? $" · no ritmo atual acaba {MeterHints.When(eta, now, tz)}"
                : m.ResetAt is { } r2 ? $" · reseta {MeterHints.Until(r2, now, tz)}"
                : ""),
    };
}
