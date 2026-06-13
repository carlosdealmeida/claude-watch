using System.Net;
using ClaudeWatch.Infrastructure;
using Xunit;

public class GitHubReleaseClientTests
{
    private const string Body =
        """{"tag_name":"v0.2.0","html_url":"https://github.com/x/claude-watch/releases/tag/v0.2.0","name":"0.2.0"}""";

    [Fact]
    public async Task Sucesso_extrai_tag_e_url()
    {
        var client = new GitHubReleaseClient(new HttpClient(new FakeHttpHandler(HttpStatusCode.OK, Body)));
        var r = await client.FetchLatestAsync(default);
        Assert.Equal("v0.2.0", r!.TagName);
        Assert.Equal("https://github.com/x/claude-watch/releases/tag/v0.2.0", r.HtmlUrl);
    }

    [Fact]
    public async Task Envia_user_agent()
    {
        var handler = new FakeHttpHandler(HttpStatusCode.OK, Body);
        await new GitHubReleaseClient(new HttpClient(handler)).FetchLatestAsync(default);
        Assert.Equal("ClaudeWatch", handler.LastRequest!.Headers.UserAgent.ToString());
    }

    [Theory]
    [InlineData(HttpStatusCode.NotFound)]   // repo sem releases
    [InlineData(HttpStatusCode.Forbidden)]  // rate-limit
    public async Task Erro_http_retorna_null(HttpStatusCode code)
    {
        var client = new GitHubReleaseClient(new HttpClient(new FakeHttpHandler(code, "{}")));
        Assert.Null(await client.FetchLatestAsync(default));
    }
}
