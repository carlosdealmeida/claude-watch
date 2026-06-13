using ClaudeWatch.Core;
using Xunit;

public class UpdateCheckerTests
{
    private const string Url = "https://github.com/x/releases/tag/v0.2.0";

    [Fact]
    public void Versao_maior_indica_update()
    {
        var s = UpdateChecker.Check(new Version(0, 1, 0), "v0.2.0", Url);
        Assert.True(s.Available);
        Assert.Equal("0.2.0", s.LatestVersion);
        Assert.Equal(Url, s.Url);
    }

    [Theory]
    [InlineData("0.1.0")]   // igual
    [InlineData("v0.1.0")]  // igual com prefixo
    [InlineData("v0.0.9")]  // menor
    public void Versao_igual_ou_menor_nao_indica_update(string tag) =>
        Assert.False(UpdateChecker.Check(new Version(0, 1, 0), tag, Url).Available);

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("nightly")]   // não parseável
    public void Tag_invalida_ou_ausente_nao_indica_update(string? tag) =>
        Assert.False(UpdateChecker.Check(new Version(0, 1, 0), tag, Url).Available);

    [Fact]
    public void Url_ausente_nao_indica_update() =>
        Assert.False(UpdateChecker.Check(new Version(0, 1, 0), "v9.9.9", null).Available);

    [Fact]
    public void Tag_sem_patch_e_tolerada()
    {
        var s = UpdateChecker.Check(new Version(0, 1, 0), "v0.2", Url);
        Assert.True(s.Available);
    }
}
