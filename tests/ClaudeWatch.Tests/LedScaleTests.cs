using ClaudeWatch.Core;
using Xunit;

public class LedScaleTests
{
    [Theory] [InlineData(0, 0)] [InlineData(42, 6)] [InlineData(96, 14)] [InlineData(100, 15)]
    public void Quantidade_acesa(int pct, int acesos) =>
        Assert.Equal(acesos, LedScale.Build(pct).Count(s => s.Lit));

    [Fact]
    public void Cor_e_por_posicao_nao_por_valor()
    {
        var s = LedScale.Build(100); // tudo aceso: ponta verde, meio âmbar, topo vermelho
        Assert.Equal(Zone.Verde, s[0].Zone);
        Assert.Equal(Zone.Ambar, s[10].Zone);    // centro 70.0
        Assert.Equal(Zone.Vermelho, s[13].Zone); // centro 90.0
    }

    [Fact]
    public void Com_aviso_de_ritmo_o_verde_vira_ambar()
    {
        // Fable 18% acabando antes do reset: os LEDs acompanham o âmbar do texto
        var s = LedScale.Build(18, warn: true);
        Assert.All(s.Where(x => x.Lit), x => Assert.Equal(Zone.Ambar, x.Zone));
    }

    [Fact]
    public void Com_aviso_de_ritmo_o_vermelho_continua_vermelho()
    {
        var s = LedScale.Build(100, warn: true);
        Assert.Equal(Zone.Ambar, s[0].Zone);
        Assert.Equal(Zone.Vermelho, s[13].Zone);
    }
}
