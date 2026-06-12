using ClaudeWatch.Core;
using Xunit;

public class TooltipFormatterTests
{
    [Fact]
    public void Formato_showroom()
    {
        var s = Snapshots.Of(42, 78, 96) with
        { FiveHour = new Meter("Sessão 5h", 42, new DateTimeOffset(2026, 6, 11, 15, 0, 0, TimeSpan.Zero)) };
        var t = TooltipFormatter.Format(s, local: false);
        Assert.Equal("5h 42% (reset 15:00) · Sem 78% · Sonnet 96%", t);
        Assert.True(t.Length <= 127);
    }

    [Fact]
    public void Sem_reset_omite_parenteses() =>
        Assert.Equal("5h 10% · Sem 20% · Sonnet 30%", TooltipFormatter.Format(Snapshots.Of(10, 20, 30), local: false));
}
