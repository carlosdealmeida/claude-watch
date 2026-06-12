using System.Net;
using ClaudeWatch.Core;
using ClaudeWatch.Credentials;
using Xunit;

public class UsageTests
{
    // TODO(carlos): substituir pelo shape real do parity e ajustar o parser.
    private const string Fixture = """
    {"five_hour":{"utilization":42.4,"resets_at":"2026-06-11T15:00:00Z"},
     "seven_day":{"utilization":78.0,"resets_at":"2026-06-18T09:00:00Z"},
     "seven_day_opus":{"utilization":96.0,"resets_at":"2026-06-18T09:00:00Z"}}
    """;

    [Fact]
    public void Parser_mapeia_os_tres_medidores()
    {
        var s = UsageResponseParser.Parse(Fixture, DateTimeOffset.UnixEpoch);
        Assert.Equal((42, 78, 96), (s.FiveHour.Pct, s.Week.Pct, s.Opus.Pct));
        Assert.Equal(SnapshotState.Ok, s.State);
        Assert.NotNull(s.FiveHour.ResetAt);
    }

    [Fact]
    public async Task Quatrocentos_e_um_vira_UnauthorizedException()
    {
        var client = new UsageClient(new HttpClient(new FakeHttpHandler(HttpStatusCode.Unauthorized, "")));
        await Assert.ThrowsAsync<UnauthorizedException>(() => client.FetchAsync("AT", default));
    }

    [Fact]
    public async Task Sucesso_envia_bearer_e_parseia()
    {
        var handler = new FakeHttpHandler(HttpStatusCode.OK, Fixture);
        var s = await new UsageClient(new HttpClient(handler)).FetchAsync("AT", default);
        Assert.Equal("Bearer", handler.LastRequest!.Headers.Authorization!.Scheme);
        Assert.Equal(96, s.Opus.Pct);
    }
}
