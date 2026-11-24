using System.Globalization;
using System.Windows;
using ClaudeWatch.Core;
using ClaudeWatch.Widget;
using Xunit;

public class SkinsTests
{
    [Theory]
    [InlineData(2, new[] { 64.0, 48.0 })]
    [InlineData(3, new[] { 64.0, 50.0, 36.0 })]
    [InlineData(4, new[] { 64.0, 52.0, 40.0, 28.0 })]
    public void Aneis_concentricos_cabem_no_quadro(int count, double[] raios)
    {
        var r = Enumerable.Range(0, count).Select(i => RingLayout.Radius(i, count)).ToArray();
        Assert.Equal(raios, r);
        // o anel de dentro ainda deixa espaço para o número no centro
        Assert.True(r[^1] - RingLayout.Thickness(count) / 2 >= 24);
    }

    [Fact]
    public void Estrela_da_coluna_segue_o_percentual()
    {
        var c = new PctToStarConverter();
        Assert.Equal(new GridLength(42, GridUnitType.Star), c.Convert(42, typeof(GridLength), null!, CultureInfo.InvariantCulture));
        Assert.Equal(new GridLength(58, GridUnitType.Star), c.Convert(42, typeof(GridLength), "rest", CultureInfo.InvariantCulture));
        Assert.Equal(new GridLength(0, GridUnitType.Star), c.Convert(null!, typeof(GridLength), null!, CultureInfo.InvariantCulture));
        Assert.Equal(new GridLength(100, GridUnitType.Star), c.Convert(null!, typeof(GridLength), "rest", CultureInfo.InvariantCulture));
    }

    [Fact]
    public void ViewModel_separa_o_critico_dos_demais()
    {
        var s = UsageResponseParser.Parse(UsageTests.Ultra, UsageTests.UltraNow, UsageTests.Brt);
        var vm = new WidgetViewModel { Snapshot = s };
        Assert.Equal("Fable", vm.Critical!.Label);
        Assert.Equal(new[] { "Semana" }, vm.Others.Select(m => m.Label));
        Assert.True(vm.HasOthers);
        Assert.Equal(2, vm.Rings.Count);
    }

    [Fact]
    public void Arco_zerado_nao_desenha() =>
        Assert.True(RingGeometry.Arc(0, 76, 76, 64).IsEmpty());
}
