namespace ClaudeWatch.Core;

public enum SnapshotState { Ok, Stale, NoCredential }

/// <summary>
/// Um limite do plano (sessão de 5h, semana, semana de um modelo...).
/// Quais existem depende do plano: no Ultra verificado não há sessão de 5h,
/// e o Fable tem um limite semanal próprio.
/// </summary>
public sealed record Meter(string Label, int Pct, DateTimeOffset? ResetAt)
{
    /// <summary>Identificador estável (kind + escopo), para testes e logs.</summary>
    public string Key { get; init; } = "";
    /// <summary>Rótulo curto para o tooltip do tray (limite de 127 caracteres).</summary>
    public string Short { get; init; } = "";
    /// <summary>Início da janela do limite; com ele e o reset dá para medir o ritmo.</summary>
    public DateTimeOffset? WindowStart { get; init; }
    /// <summary>Quando o limite acaba se o ritmo atual continuar; nulo se dá até o reset.</summary>
    public DateTimeOffset? RunsOutAt { get; init; }
    /// <summary>Linha curta mostrada sob o medidor ("↻ dom 09:00", "⚠ acaba sáb 01:07").</summary>
    public string? Hint { get; init; }

    public Zone Zone => ZoneRules.From(Pct);
    public bool Exhausted => Pct >= 100;
    /// <summary>Esgotado, ou vai esgotar antes do reset no ritmo atual.</summary>
    public bool Warn => Exhausted || RunsOutAt is not null;
}

public sealed record UsageSnapshot(
    IReadOnlyList<Meter> Meters, DateTimeOffset CollectedAt, SnapshotState State)
{
    private static readonly Meter None = new("—", 0, null) { Short = "—" };

    public Meter Worst => Meters.Count == 0 ? None : Meters.MaxBy(m => m.Pct)!;

    public Meter? Find(string key) => Meters.FirstOrDefault(m => m.Key == key);
}
