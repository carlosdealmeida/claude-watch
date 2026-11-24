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

    /// <summary>Fatia de outro limite dentro deste (o Fable dentro da Semana), em % deste.</summary>
    public int? Part { get; init; }
    /// <summary>Teto dessa fatia, em % deste (o Fable pode usar até 50% da Semana).</summary>
    public int? PartCap { get; init; }
    /// <summary>Nome do limite da fatia ("Fable").</summary>
    public string? PartLabel { get; init; }

    public string? PartText => Part is { } p && PartLabel is { } l ? $"{l} {p} de {Pct}%" : null;

    public Zone Zone => ZoneRules.From(Pct);
    /// <summary>A zona do percentual, mas âmbar quando o ritmo esgota antes do reset.</summary>
    public Zone DisplayZone => Warn && Zone == Zone.Verde ? Zone.Ambar : Zone;
    public bool Exhausted => Pct >= 100;
    /// <summary>Esgotado, ou vai esgotar antes do reset no ritmo atual.</summary>
    public bool Warn => Exhausted || RunsOutAt is not null;
}

/// <summary>Uma linha de "de onde veio o consumo da semana" (Claude Code, Cowork, chat).</summary>
public sealed record BreakdownRow(string Label, int Pct);

public sealed record UsageSnapshot(
    IReadOnlyList<Meter> Meters, DateTimeOffset CollectedAt, SnapshotState State)
{
    private static readonly Meter None = new("—", 0, null) { Short = "—" };

    /// <summary>Só as linhas com consumo, na ordem da resposta.</summary>
    public IReadOnlyList<BreakdownRow> Breakdown { get; init; } = [];

    public string? BreakdownText => Breakdown.Count == 0 ? null
        : "Semana: " + string.Join(" · ", Breakdown.Select(b => $"{b.Label} {b.Pct}%"));

    public Meter Worst => Meters.Count == 0 ? None : Meters.MaxBy(m => m.Pct)!;

    /// <summary>
    /// O medidor que mais importa agora: esgotado, depois o que acaba antes do reset,
    /// depois o maior percentual. É o que o ícone do tray e o skin compacto mostram.
    /// </summary>
    public Meter Critical => Meters.Count == 0 ? None : Meters
        .OrderByDescending(m => m.Exhausted).ThenByDescending(m => m.Warn).ThenByDescending(m => m.Pct)
        .First();

    public Meter? Find(string key) => Meters.FirstOrDefault(m => m.Key == key);
}
