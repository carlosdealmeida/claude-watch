using System.Net;
using ClaudeWatch.Core;
using ClaudeWatch.Credentials;
using Xunit;

public class UsageTests
{
    // Shape real do endpoint api.anthropic.com/api/oauth/usage (fixture do ATLAS):
    // buckets podem ser null (cota inativa no plano) e há buckets extras ignorados.
    private const string Fixture = """
    {"five_hour":{"utilization":42.4,"resets_at":"2026-06-11T15:00:00Z"},
     "seven_day":{"utilization":78.0,"resets_at":"2026-06-18T09:00:00Z"},
     "seven_day_oauth_apps":null,
     "seven_day_opus":null,
     "seven_day_sonnet":{"utilization":96.0,"resets_at":"2026-06-18T09:00:00Z"},
     "extra_usage":{"is_enabled":false,"monthly_limit":null,"used_credits":null,"utilization":null,"currency":null}}
    """;

    [Fact]
    public void Parser_mapeia_os_tres_medidores()
    {
        var s = UsageResponseParser.Parse(Fixture, DateTimeOffset.UnixEpoch);
        Assert.Equal((42, 78, 96), (s.FiveHour.Pct, s.Week.Pct, s.Sonnet.Pct));
        Assert.Equal(SnapshotState.Ok, s.State);
        Assert.NotNull(s.FiveHour.ResetAt);
    }

    [Fact]
    public void Bucket_nulo_ou_sem_utilization_vira_zero()
    {
        const string json = """
        {"five_hour":{"utilization":42.0,"resets_at":"2026-06-11T15:00:00Z"},
         "seven_day":null,
         "seven_day_sonnet":{"utilization":null,"resets_at":null}}
        """;
        var s = UsageResponseParser.Parse(json, DateTimeOffset.UnixEpoch);
        Assert.Equal((42, 0, 0), (s.FiveHour.Pct, s.Week.Pct, s.Sonnet.Pct));
        Assert.Null(s.Week.ResetAt);
        Assert.Null(s.Sonnet.ResetAt);
    }

    [Fact]
    public async Task Quatrocentos_e_um_vira_UnauthorizedException()
    {
        var client = new UsageClient(new HttpClient(new FakeHttpHandler(HttpStatusCode.Unauthorized, "")));
        await Assert.ThrowsAsync<UnauthorizedException>(() => client.FetchAsync("AT", default));
    }

    [Fact]
    public async Task Sucesso_envia_bearer_e_beta_e_parseia()
    {
        var handler = new FakeHttpHandler(HttpStatusCode.OK, Fixture);
        var s = await new UsageClient(new HttpClient(handler)).FetchAsync("AT", default);
        Assert.Equal("Bearer", handler.LastRequest!.Headers.Authorization!.Scheme);
        Assert.Equal("oauth-2025-04-20", handler.LastRequest!.Headers.GetValues("anthropic-beta").Single());
        Assert.Equal(96, s.Sonnet.Pct);
    }
}
