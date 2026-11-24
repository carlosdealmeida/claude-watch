using ClaudeWatch.Core;
using Xunit;

public class MeterHintsTests
{
    private static readonly DateTimeOffset Start = new(2026, 11, 23, 12, 0, 0, TimeSpan.Zero);
    private static readonly DateTimeOffset Reset = Start.AddDays(7);

    private static Meter Week(int pct) => new("Semana", pct, Reset) { WindowStart = Start };

    [Fact]
    public void Ritmo_que_cabe_na_janela_nao_alerta() =>
        // 1 dia de 7 com 10%: chegaria a 100% em 10 dias
        Assert.Null(MeterHints.RunsOutAt(Week(10), Start.AddDays(1)));

    [Fact]
    public void Ritmo_acima_da_janela_projeta_o_fim() =>
        // 1 dia com 25%: chega a 100% em 4 dias
        Assert.Equal(Start.AddDays(4), MeterHints.RunsOutAt(Week(25), Start.AddDays(1)));

    [Fact]
    public void Cedo_demais_nao_projeta() =>
        // 2h de uma semana: 40% aqui é ruído (um único prompt grande)
        Assert.Null(MeterHints.RunsOutAt(Week(40), Start.AddHours(2)));

    [Fact]
    public void Sem_janela_nao_projeta() =>
        Assert.Null(MeterHints.RunsOutAt(new Meter("X", 50, Reset), Start.AddDays(1)));

    [Fact]
    public void Esgotado_diz_quando_volta()
    {
        var m = MeterHints.Annotate(Week(100), Start.AddDays(3), TimeZoneInfo.Utc);
        Assert.True(m.Warn);
        Assert.Equal("esgotado · volta seg 12:00", m.Hint);
    }

    [Theory]
    [InlineData(133, "em 2h13")]
    [InlineData(125, "em 2h05")]
    [InlineData(180, "em 3h")]
    [InlineData(45, "em 45min")]
    [InlineData(0.5, "em 1min")]         // arredonda para cima: nunca promete antes da hora
    [InlineData(1439, "em 23h59")]
    [InlineData(1440, "qua 09:00")]      // 24h ou mais: o horário diz mais
    [InlineData(-5, "08:55")]            // já passou: dado velho, fica o horário
    public void Until_conta_so_quando_falta_menos_de_24h(double minutos, string esperado)
    {
        var now = new DateTimeOffset(2026, 11, 24, 9, 0, 0, TimeSpan.Zero); // terça
        Assert.Equal(esperado, MeterHints.Until(now.AddMinutes(minutos), now, TimeZoneInfo.Utc));
    }

    [Fact]
    public void Reset_perto_vira_contagem()
    {
        var now = Reset.AddMinutes(-133);
        Assert.Equal("↻ em 2h13", MeterHints.Annotate(Week(10), now, TimeZoneInfo.Utc).Hint);
    }

    [Fact]
    public void Esgotado_perto_do_reset_conta_quanto_falta()
    {
        var m = MeterHints.Annotate(Week(100), Reset.AddMinutes(-90), TimeZoneInfo.Utc);
        Assert.Equal("esgotado · volta em 1h30", m.Hint);
    }

    [Fact]
    public void Refresh_recalcula_a_linha_com_a_hora_nova()
    {
        var s = new UsageSnapshot([MeterHints.Annotate(Week(10), Reset.AddHours(-3), TimeZoneInfo.Utc)],
            Reset.AddHours(-3), SnapshotState.Stale);
        var depois = MeterHints.Refresh(s, Reset.AddMinutes(-40), TimeZoneInfo.Utc);
        Assert.Equal("↻ em 40min", depois.Meters[0].Hint);
        Assert.Equal(SnapshotState.Stale, depois.State);
    }

    [Theory]
    [InlineData(0, "15:00")]           // hoje
    [InlineData(2, "qui 15:00")]       // nesta semana
    [InlineData(9, "03/12 15:00")]     // depois
    public void When_escolhe_o_formato(int dias, string esperado)
    {
        var now = new DateTimeOffset(2026, 11, 24, 9, 0, 0, TimeSpan.Zero); // terça
        Assert.Equal(esperado, MeterHints.When(now.AddDays(dias).AddHours(6), now, TimeZoneInfo.Utc));
    }
}
