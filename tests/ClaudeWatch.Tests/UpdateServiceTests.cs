using System.Net;
using ClaudeWatch.Core;
using ClaudeWatch.Infrastructure;
using Xunit;

public class UpdateServiceTests
{
    private static UpdateService Service(HttpStatusCode code, string body, Action<UpdateStatus> publish) =>
        new(new Version(0, 1, 0),
            new GitHubReleaseClient(new HttpClient(new FakeHttpHandler(code, body))),
            publish, _ => { });

    [Fact]
    public async Task Versao_nova_publica_disponivel()
    {
        UpdateStatus? pub = null;
        var svc = Service(HttpStatusCode.OK,
            """{"tag_name":"v9.9.9","html_url":"https://x/r"}""", s => pub = s);
        var status = await svc.CheckAsync(default);
        Assert.True(status.Available);
        Assert.Equal("9.9.9", status.LatestVersion);
        Assert.True(pub!.Available);
    }

    [Fact]
    public async Task Sem_release_publica_indisponivel()
    {
        UpdateStatus? pub = null;
        var svc = Service(HttpStatusCode.NotFound, "{}", s => pub = s);
        var status = await svc.CheckAsync(default);
        Assert.False(status.Available);
        Assert.False(pub!.Available);
    }
}
