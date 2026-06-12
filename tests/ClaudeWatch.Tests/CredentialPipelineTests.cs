using ClaudeWatch.Credentials;
using ClaudeWatch.Infrastructure;
using Xunit;

public sealed class FakeCredFile(string? json) : ICredentialFile
{
    public string? Json = json;
    public string? ReadOrNull() => Json;
    public event Action? Changed { add { } remove { } }
}

public class CredentialPipelineTests
{
    private static readonly DateTimeOffset Now = DateTimeOffset.UnixEpoch.AddDays(1000);
    private static string CredJson(long expMs, string rt = "RT") =>
        $"{{\"claudeAiOauth\":{{\"accessToken\":\"FILE\",\"refreshToken\":\"{rt}\",\"expiresAt\":{expMs}}}}}";
    private static string TempDir() => Directory.CreateDirectory(
        Path.Combine(Path.GetTempPath(), "cw-" + Guid.NewGuid())).FullName;

    private static CredentialPipeline Pipe(ICredentialFile f, TokenCache cache,
        System.Net.HttpStatusCode code, string body) =>
        new(f, cache, new OAuthRefreshClient(new HttpClient(new FakeHttpHandler(code, body))), _ => { });

    [Fact]
    public async Task Arquivo_valido_nao_refresca()
    {
        var p = Pipe(new FakeCredFile(CredJson(Now.AddHours(1).ToUnixTimeMilliseconds())),
            new TokenCache(TempDir()), System.Net.HttpStatusCode.InternalServerError, "");
        Assert.Equal("FILE", await p.GetAccessTokenAsync(Now, default));
        Assert.False(p.NoCredential);
    }

    [Fact]
    public async Task Expirado_refresca_e_cacheia()
    {
        var cache = new TokenCache(TempDir());
        var p = Pipe(new FakeCredFile(CredJson(Now.AddMinutes(-1).ToUnixTimeMilliseconds())), cache,
            System.Net.HttpStatusCode.OK, """{"access_token":"NEW","expires_in":3600}""");
        Assert.Equal("NEW", await p.GetAccessTokenAsync(Now, default));
        Assert.Equal("NEW", cache.Load()!.AccessToken);
    }

    [Fact]
    public async Task Refresh_rejeitado_limpa_cache_e_marca_NoCredential()
    {
        var cache = new TokenCache(TempDir());
        cache.Save(new OAuthCredential("OLD", null, Now.AddMinutes(-5)));
        var p = Pipe(new FakeCredFile(CredJson(Now.AddMinutes(-1).ToUnixTimeMilliseconds())), cache,
            System.Net.HttpStatusCode.BadRequest, "{}");
        Assert.Null(await p.GetAccessTokenAsync(Now, default));
        Assert.True(p.NoCredential);
        Assert.Null(cache.Load());
    }

    [Fact]
    public async Task Sem_arquivo_e_NoCredential()
    {
        var p = Pipe(new FakeCredFile(null), new TokenCache(TempDir()),
            System.Net.HttpStatusCode.OK, "{}");
        Assert.Null(await p.GetAccessTokenAsync(Now, default));
        Assert.True(p.NoCredential);
    }
}

public class CredentialPathsTests
{
    [Fact]
    public void Primario_existente_vence_fallback_wsl()
    {
        var home = Directory.CreateDirectory(Path.Combine(Path.GetTempPath(), "h-" + Guid.NewGuid())).FullName;
        var primary = Path.Combine(home, ".claude", ".credentials.json");
        Directory.CreateDirectory(Path.GetDirectoryName(primary)!);
        File.WriteAllText(primary, "{}");
        Assert.Equal(primary, CredentialPaths.Resolve(home, new Settings { WslCredentialsPath = @"\\wsl$\x" }));
    }

    [Fact]
    public void Sem_primario_usa_wsl_configurado()
    {
        var home = Directory.CreateDirectory(Path.Combine(Path.GetTempPath(), "h-" + Guid.NewGuid())).FullName;
        Assert.Equal(@"\\wsl$\x", CredentialPaths.Resolve(home, new Settings { WslCredentialsPath = @"\\wsl$\x" }));
    }
}
