using ClaudeWatch.Credentials;
using Xunit;

public class TokenCacheTests
{
    private static string TempDir() => Directory.CreateDirectory(
        Path.Combine(Path.GetTempPath(), "cw-" + Guid.NewGuid())).FullName;

    [Fact]
    public void Round_trip()
    {
        var cache = new TokenCache(TempDir());
        cache.Save(new OAuthCredential("AT", null, DateTimeOffset.UnixEpoch.AddDays(1)));
        var c = cache.Load()!;
        Assert.Equal("AT", c.AccessToken);
    }

    [Fact]
    public void Bytes_adulterados_retornam_null()
    {
        var dir = TempDir(); var cache = new TokenCache(dir);
        cache.Save(new OAuthCredential("AT", null, DateTimeOffset.UnixEpoch));
        File.WriteAllBytes(Path.Combine(dir, "token.bin"), [1, 2, 3]);
        Assert.Null(cache.Load());
    }

    [Fact] public void Clear_e_ausente_sao_null()
    { var c = new TokenCache(TempDir()); c.Clear(); Assert.Null(c.Load()); }
}
