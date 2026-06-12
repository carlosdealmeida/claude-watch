using ClaudeWatch.Credentials;
using Xunit;

public class CredentialsParserTests
{
    private const string Fixture = """
    {"claudeAiOauth":{"accessToken":"AT","refreshToken":"RT","expiresAt":1780000000000,
     "scopes":["x"],"campoDesconhecido":{"a":1}},"outroBloco":true}
    """;

    [Fact]
    public void Parse_ignora_campos_desconhecidos()
    {
        var c = ClaudeCodeCredentialsParser.TryParse(Fixture)!;
        Assert.Equal(("AT", "RT"), (c.AccessToken, c.RefreshToken));
        Assert.Equal(DateTimeOffset.FromUnixTimeMilliseconds(1780000000000), c.ExpiresAt);
    }

    [Theory] [InlineData("{nope")] [InlineData("{}")] [InlineData("""{"claudeAiOauth":{"refreshToken":"RT"}}""")]
    public void Invalido_retorna_null(string json) =>
        Assert.Null(ClaudeCodeCredentialsParser.TryParse(json));
}
