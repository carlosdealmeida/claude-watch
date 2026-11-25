using ClaudeWatch.Core;
using Xunit;

public class UsageAlertsTests
{
    // terça 24/11 09:00 UTC; a semana reseta segunda 30/11 09:00
    private static readonly DateTimeOffset Now = new(2026, 11, 24, 9, 0, 0, TimeSpan.Zero);
    private static readonly DateTimeOffset Reset = new(2026, 11, 30, 9, 0, 0, TimeSpan.Zero);

    private static Meter Week(int pct, DateTimeOffset? reset = null, DateTimeOffset? runsOut = null) =>
        new("Semana", pct, reset ?? Reset) { Key = "weekly_all", RunsOutAt = runsOut };

    private static UsageSnapshot Snap(Meter m, SnapshotState st = SnapshotState.Ok) => new([m], Now, st);

    private static IReadOnlyList<string> Check(UsageAlerts a, Meter m, SnapshotState st = SnapshotState.Ok) =>
        a.Check(Snap(m, st), Now, TimeZoneInfo.Utc);

    [Fact]
    public void Primeira_leitura_nao_avisa_o_que_ja_vale()
    {
        var a = new UsageAlerts();
        Assert.Empty(Check(a, Week(75)));
        Assert.Empty(Check(a, Week(78)));
    }

    [Fact]
    public void Cruzar_70_avisa_uma_vez()
    {
        var a = new UsageAlerts();
        Check(a, Week(60));
        Assert.Equal(["Semana passou de 70% (72%) · reseta seg 09:00"], Check(a, Week(72)));
        Assert.Empty(Check(a, Week(74)));
    }

    [Fact]
    public void Cruzar_90_avisa_de_novo()
    {
        var a = new UsageAlerts();
        Check(a, Week(60));
        Check(a, Week(72));
        Assert.Equal(["Semana passou de 90% (91%) · reseta seg 09:00"], Check(a, Week(91)));
    }

    [Fact]
    public void Salto_de_zona_da_um_aviso_so_o_mais_grave()
    {
        var a = new UsageAlerts();
        Check(a, Week(60));
        Assert.Equal(["Semana passou de 90% (95%) · reseta seg 09:00"], Check(a, Week(95)));
        Assert.Empty(Check(a, Week(96)));
    }

    [Fact]
    public void Ritmo_avisa_antes_dos_70_e_uma_vez_por_janela()
    {
        var a = new UsageAlerts();
        Check(a, Week(10));
        var eta = new DateTimeOffset(2026, 11, 29, 1, 16, 0, TimeSpan.Zero);
        Assert.Equal(["Semana em 12%: no ritmo atual acaba dom 01:16, antes do reset"],
            Check(a, Week(12, runsOut: eta)));
        Check(a, Week(12));                      // a projeção some quando a pessoa pausa...
        Assert.Empty(Check(a, Week(13, runsOut: eta))); // ...e volta: não avisa de novo
    }

    [Fact]
    public void Passar_de_70_com_ritmo_diz_quando_acaba()
    {
        var a = new UsageAlerts();
        var eta = new DateTimeOffset(2026, 11, 29, 1, 16, 0, TimeSpan.Zero);
        Check(a, Week(60, runsOut: eta));
        Assert.Equal(["Semana passou de 70% (72%) · no ritmo atual acaba dom 01:16"],
            Check(a, Week(72, runsOut: eta)));
    }

    [Fact]
    public void Esgotar_diz_quando_volta()
    {
        var a = new UsageAlerts();
        Check(a, Week(95, reset: Now.AddHours(3)));
        Assert.Equal(["Semana esgotou · volta em 3h"], Check(a, Week(100, reset: Now.AddHours(3))));
    }

    [Fact]
    public void Depois_do_reset_volta_a_avisar()
    {
        var a = new UsageAlerts();
        Check(a, Week(60));
        Check(a, Week(91));
        var next = Reset.AddDays(7);
        Check(a, Week(5, reset: next));
        Assert.Equal(["Semana passou de 70% (72%) · reseta 07/12 09:00"], Check(a, Week(72, reset: next)));
    }

    [Fact]
    public void Reset_sem_proximo_horario_tambem_abre_janela_nova()
    {
        // A statusline zera o medidor e tira o reset quando a janela vira.
        var a = new UsageAlerts();
        Check(a, Week(60));
        Check(a, Week(91));
        Check(a, new Meter("Semana", 0, null) { Key = "weekly_all" });
        Check(a, Week(60, reset: Reset.AddDays(7)));
        Assert.Single(Check(a, Week(72, reset: Reset.AddDays(7))));
    }

    [Fact]
    public void Dado_velho_nao_avisa_nem_conta_como_visto()
    {
        var a = new UsageAlerts();
        Check(a, Week(60));
        Assert.Empty(Check(a, Week(72), SnapshotState.Stale));
        Assert.Single(Check(a, Week(72)));
    }

    [Fact]
    public void Cada_medidor_avisa_por_si()
    {
        var a = new UsageAlerts();
        Meter Fable(int pct) => new("Fable", pct, Reset) { Key = "weekly_scoped:fable" };
        a.Check(new([Week(60), Fable(60)], Now, SnapshotState.Ok), Now, TimeZoneInfo.Utc);
        var msgs = a.Check(new([Week(72), Fable(91)], Now, SnapshotState.Ok), Now, TimeZoneInfo.Utc);
        Assert.Equal(["Semana passou de 70% (72%) · reseta seg 09:00",
                      "Fable passou de 90% (91%) · reseta seg 09:00"], msgs);
    }
}
