using ClaudeWatch.Core;

namespace ClaudeWatch.Infrastructure;

/// <summary>
/// Modo demo opt-in (env var CLAUDEWATCH_SHOWROOM=ok|stale|nocred|ultra): injeta um
/// snapshot fixo sem tocar a rede, para QA visual dos skins/estados
/// e para tirar screenshots sem expor o uso real. Default: off.
///
/// - ok: plano com sessão de 5h (Max) — 5h 42%, Semana 78%, Fable 96%
/// - ultra: Ultra verificado, sem sessão de 5h — Semana 12%, Fable 18% acabando antes do reset
/// </summary>
public static class Showroom
{
    public static string? Mode => Environment.GetEnvironmentVariable("CLAUDEWATCH_SHOWROOM");

    public static UsageSnapshot Snapshot() => Mode?.ToLowerInvariant() switch
    {
        "stale" => Max(SnapshotState.Stale),
        "nocred" => Max(SnapshotState.NoCredential),
        "ultra" => Ultra(),
        _ => Max(SnapshotState.Ok),
    };

    private static UsageSnapshot Max(SnapshotState state)
    {
        var now = DateTimeOffset.Now;
        return Build(now, state,
            new Meter("Sessão 5h", 42, now.AddHours(2)) { Key = "five_hour", Short = "5h", WindowStart = now.AddHours(-3) },
            new Meter("Semana", 78, now.AddDays(3)) { Key = "weekly_all", Short = "Sem", WindowStart = now.AddDays(-4) },
            new Meter("Fable", 96, now.AddDays(3)) { Key = "weekly_scoped:fable", Short = "Fable", WindowStart = now.AddDays(-4) });
    }

    private static UsageSnapshot Ultra()
    {
        var now = DateTimeOffset.Now;
        var start = now.AddDays(-1);
        return Build(now, SnapshotState.Ok,
            new Meter("Semana", 12, start.AddDays(7)) { Key = "weekly_all", Short = "Sem", WindowStart = start },
            new Meter("Fable", 18, start.AddDays(7)) { Key = "weekly_scoped:fable", Short = "Fable", WindowStart = start });
    }

    private static UsageSnapshot Build(DateTimeOffset now, SnapshotState state, params Meter[] meters) =>
        new(meters.Select(m => MeterHints.Annotate(m, now, TimeZoneInfo.Local)).ToList(), now, state);
}
