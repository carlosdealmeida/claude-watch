using ClaudeWatch.Core;

namespace ClaudeWatch.Infrastructure;

/// <summary>
/// Modo demo opt-in (env var CLAUDEWATCH_SHOWROOM=ok|stale|nocred|ultra|alerta): injeta um
/// snapshot fixo sem tocar a rede, para QA visual dos skins/estados
/// e para tirar screenshots sem expor o uso real. Default: off.
///
/// - ok: plano com sessão de 5h (Max) — 5h 42%, Semana 78%, Fable 96%
/// - ultra: Ultra verificado, sem sessão de 5h — Semana 12%, Fable 18% acabando antes do reset
/// - alerta: sobe a cada leitura, para ver os avisos de limite
/// </summary>
public static class Showroom
{
    public static string? Mode => Environment.GetEnvironmentVariable("CLAUDEWATCH_SHOWROOM");

    public static UsageSnapshot Snapshot() => Mode?.ToLowerInvariant() switch
    {
        "stale" => Max(SnapshotState.Stale),
        "nocred" => Max(SnapshotState.NoCredential),
        "ultra" => Ultra(),
        "alerta" => Alerta(),
        _ => Max(SnapshotState.Ok),
    };

    private static int _step;

    /// <summary>
    /// Sobe a cada leitura ("Atualizar agora" no tray adianta), para ver os avisos:
    /// Semana 60 → 72 → 91 → 100%, e o Fable entra no ritmo de acabar antes do reset no 2º passo.
    /// </summary>
    private static UsageSnapshot Alerta()
    {
        int[] semana = [60, 72, 91, 100], fable = [1, 18, 25, 30];
        var i = Math.Min(_step++, semana.Length - 1);
        var now = DateTimeOffset.Now;
        var start = now.AddDays(-1);
        return Build(now, SnapshotState.Ok,
            new Meter("Semana", semana[i], now.AddHours(3)) { Key = "weekly_all", Short = "Sem", WindowStart = now.AddDays(-7).AddHours(3) },
            new Meter("Fable", fable[i], start.AddDays(7)) { Key = "weekly_scoped:fable", Short = "Fable", WindowStart = start });
    }

    private static UsageSnapshot Max(SnapshotState state)
    {
        var now = DateTimeOffset.Now;
        return Build(now, state,
            new Meter("Sessão 5h", 42, now.AddHours(2)) { Key = "five_hour", Short = "5h", WindowStart = now.AddHours(-3) },
            new Meter("Semana", 78, now.AddDays(3))
            { Key = "weekly_all", Short = "Sem", WindowStart = now.AddDays(-4), Part = 39, PartCap = 50, PartLabel = "Fable" },
            new Meter("Fable", 96, now.AddDays(3)) { Key = "weekly_scoped:fable", Short = "Fable", WindowStart = now.AddDays(-4) });
    }

    private static UsageSnapshot Ultra()
    {
        var now = DateTimeOffset.Now;
        var start = now.AddDays(-1);
        return Build(now, SnapshotState.Ok,
            new Meter("Semana", 12, start.AddDays(7))
            { Key = "weekly_all", Short = "Sem", WindowStart = start, Part = 9, PartCap = 50, PartLabel = "Fable" },
            new Meter("Fable", 18, start.AddDays(7)) { Key = "weekly_scoped:fable", Short = "Fable", WindowStart = start });
    }

    private static UsageSnapshot Build(DateTimeOffset now, SnapshotState state, params Meter[] meters) =>
        new(meters.Select(m => MeterHints.Annotate(m, now, TimeZoneInfo.Local)).ToList(), now, state)
        {
            Breakdown = [new BreakdownRow("Claude Code", 92), new BreakdownRow("Cowork", 8)],
        };
}
