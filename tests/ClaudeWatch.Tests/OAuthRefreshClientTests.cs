using System.Net;
using ClaudeWatch.Credentials;
using Xunit;

public class OAuthRefreshClientTests
{
    private static OAuthRefreshClient Client(HttpStatusCode code, string body) =>
        new(new HttpClient(new FakeHttpHandler(code, body)));

    [Fact]
    public async Task Sucesso_retorna_credencial_com_expiracao()
    {
        var r = await Client(HttpStatusCode.OK,
            """{"access_token":"NEW","expires_in":3600,"refresh_token":"RT"}""").RefreshAsync("RT", default);
        Assert.Equal("NEW", r.Credential!.AccessToken);
        Assert.False(r.Rejected);
        Assert.False(r.RotationDetected);
    }

    [Fact]
    public async Task Refresh_token_diferente_sinaliza_rotacao()
    {
        var r = await Client(HttpStatusCode.OK,
            """{"access_token":"NEW","expires_in":60,"refresh_token":"OUTRO"}""").RefreshAsync("RT", default);
        Assert.True(r.RotationDetected);
    }

    [Fact]
    public async Task Quatrocentos_e_rejeicao()
    {
        var r = await Client(HttpStatusCode.BadRequest, "{}").RefreshAsync("RT", default);
        Assert.True(r.Rejected);
        Assert.Null(r.Credential);
    }

    [Fact]
    public async Task Envia_form_urlencoded_com_client_id()
    {
        var handler = new FakeHttpHandler(HttpStatusCode.OK, """{"access_token":"NEW","expires_in":3600}""");
        await new OAuthRefreshClient(new HttpClient(handler)).RefreshAsync("RT", default);
        Assert.Equal("application/x-www-form-urlencoded", handler.LastContentType);
        Assert.Contains("grant_type=refresh_token", handler.LastBody);
        Assert.Contains("client_id=9d1c250a", handler.LastBody);
    }
}
