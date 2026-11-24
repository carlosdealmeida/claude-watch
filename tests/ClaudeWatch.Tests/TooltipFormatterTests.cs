using ClaudeWatch.Core;
using Xunit;

public class TooltipFormatterTests
{
    [Fact]
    public void Ultra_mostra_quando_o_fable_acaba()
    {
        var s = UsageResponseParser.Parse(UsageTests.Ultra, UsageTests.UltraNow, UsageTests.Brt);
        Assert.Equal("Sem 12% · Fable 18% (acaba dom 01:16)", TooltipFormatter.Format(s, UsageTests.Brt));
    }

    [Fact]
    public void Sem_alerta_mostra_o_reset_do_primeiro()
    {
        var now = new DateTimeOffset(2026, 6, 11, 12, 0, 0, TimeSpan.Zero);
        var s = new UsageSnapshot(
            [new Meter("Sessão 5h", 42, now.AddHours(3)) { Short = "5h" },
             new Meter("Semana", 20, now.AddDays(3)) { Short = "Sem" }],
            now, SnapshotState.Ok);
        Assert.Equal("5h 42% (reset em 3h) · Sem 20%", TooltipFormatter.Format(s, TimeZoneInfo.Utc));
    }

    [Fact]
    public void Conta_a_partir_da_hora_passada_e_nao_da_coleta()
    {
        var coleta = new DateTimeOffset(2026, 6, 11, 12, 0, 0, TimeSpan.Zero);
        var s = new UsageSnapshot([new Meter("Sessão 5h", 42, coleta.AddHours(3)) { Short = "5h" }],
            coleta, SnapshotState.Ok);
        Assert.Equal("5h 42% (reset em 1h)", TooltipFormatter.Format(s, TimeZoneInfo.Utc, coleta.AddHours(2)));
    }

    [Fact]
    public void Nunca_passa_de_127_caracteres()
    {
        var meters = Enumerable.Range(0, 12)
            .Select(i => new Meter($"Limite {i}", i, null) { Short = $"Limite comprido {i}" }).ToList();
        var t = TooltipFormatter.Format(new UsageSnapshot(meters, DateTimeOffset.UnixEpoch, SnapshotState.Ok));
        Assert.True(t.Length <= 127);
    }
}
