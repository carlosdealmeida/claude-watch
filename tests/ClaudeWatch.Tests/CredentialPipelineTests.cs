using ClaudeWatch.Core;
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
    private static string CredJson(long expMs, string access = "FILE") =>
        $"{{\"claudeAiOauth\":{{\"accessToken\":\"{access}\",\"refreshToken\":\"RT\",\"expiresAt\":{expMs}}}}}";

    [Fact]
    public async Task Access_token_valido_e_usado()
    {
        var p = new CredentialPipeline(new FakeCredFile(CredJson(Now.AddHours(1).ToUnixTimeMilliseconds())));
        var r = await p.GetAccessTokenAsync(Now, default);
        Assert.Equal("FILE", r.AccessToken);
        Assert.Equal(SnapshotState.Ok, r.State);
    }

    [Fact]
    public async Task Access_token_expirado_fica_Stale_e_NUNCA_renova()
    {
        // Token expirado: o app é somente-leitura, então NÃO pode renovar —
        // renovar rotacionaria o refresh token compartilhado e deslogaria o Claude Code.
        var p = new CredentialPipeline(new FakeCredFile(CredJson(Now.AddMinutes(-1).ToUnixTimeMilliseconds())));
        var r = await p.GetAccessTokenAsync(Now, default);
        Assert.Null(r.AccessToken);
        Assert.Equal(SnapshotState.Stale, r.State);
    }

    [Fact]
    public async Task Sem_arquivo_e_NoCredential()
    {
        var p = new CredentialPipeline(new FakeCredFile(null));
        var r = await p.GetAccessTokenAsync(Now, default);
        Assert.Null(r.AccessToken);
        Assert.Equal(SnapshotState.NoCredential, r.State);
    }

    [Fact]
    public async Task Arquivo_sem_accessToken_e_NoCredential()
    {
        var p = new CredentialPipeline(new FakeCredFile("{\"claudeAiOauth\":{\"refreshToken\":\"RT\",\"expiresAt\":0}}"));
        var r = await p.GetAccessTokenAsync(Now, default);
        Assert.Null(r.AccessToken);
        Assert.Equal(SnapshotState.NoCredential, r.State);
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
