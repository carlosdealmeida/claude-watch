using ClaudeWatch.Core;

namespace ClaudeWatch.Infrastructure;

/// <summary>
/// Modo demo opt-in (env var CLAUDEWATCH_SHOWROOM=ok|stale|nocred): injeta o
/// snapshot canônico 42/78/96 sem tocar a rede, para QA visual dos skins/estados
/// enquanto os endpoints reais (TODO carlos) não estão preenchidos. Default: off.
/// </summary>
public static class Showroom
{
    public static string? Mode => Environment.GetEnvironmentVariable("CLAUDEWATCH_SHOWROOM");

    public static UsageSnapshot Snapshot() => Mode?.ToLowerInvariant() switch
    {
        "stale" => Demo(SnapshotState.Stale),
        "nocred" => Demo(SnapshotState.NoCredential),
        _ => Demo(SnapshotState.Ok),
    };

    private static UsageSnapshot Demo(SnapshotState state) => new(
        new Meter("Sessão 5h", 42, DateTimeOffset.Now.AddHours(2)),
        new Meter("Semana", 78, DateTimeOffset.Now.AddDays(3)),
        new Meter("Opus", 96, DateTimeOffset.Now.AddDays(3)),
        DateTimeOffset.UtcNow, state);
}
