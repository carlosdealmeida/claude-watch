using ClaudeWatch.Core;
using Xunit;

public class ZoneRulesTests
{
    [Theory]
    [InlineData(0, Zone.Verde)] [InlineData(69, Zone.Verde)]
    [InlineData(70, Zone.Ambar)] [InlineData(89, Zone.Ambar)]
    [InlineData(90, Zone.Vermelho)] [InlineData(100, Zone.Vermelho)]
    public void Regua_70_90(int pct, Zone esperada) => Assert.Equal(esperada, ZoneRules.From(pct));

    [Fact]
    public void Worst_e_o_maior_percentual()
    {
        var s = Snapshots.Of(42, 78, 96);
        Assert.Equal("Opus", s.Worst.Label);
    }
}

public static class Snapshots
{
    public static UsageSnapshot Of(int h5, int sem, int opus, SnapshotState st = SnapshotState.Ok) =>
        new(new Meter("Sessão 5h", h5, null), new Meter("Semana", sem, null),
            new Meter("Opus", opus, null), DateTimeOffset.UtcNow, st);
}
