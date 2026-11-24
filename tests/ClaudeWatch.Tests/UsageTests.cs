using System.Net;
using ClaudeWatch.Core;
using ClaudeWatch.Credentials;
using Xunit;

public class UsageTests
{
    internal static readonly TimeZoneInfo Brt =
        TimeZoneInfo.CreateCustomTimeZone("BRT", TimeSpan.FromHours(-3), "BRT", "BRT");

    // Resposta real de uma conta Ultra verificada (nov/2026), sem os buckets irrelevantes:
    // sem sessão de 5h, semana em 12% e o limite semanal do Fable em 18%.
    internal const string Ultra = """
    {"five_hour":null,
     "seven_day":{"utilization":12.0,"resets_at":"2026-11-30T12:00:00.175587+00:00"},
     "seven_day_sonnet":null,
     "seven_day_opus":null,
     "extra_usage":{"is_enabled":false},
     "limits":[
       {"kind":"weekly_all","group":"weekly","percent":12,"severity":"normal",
        "resets_at":"2026-11-30T12:00:00.175587+00:00","scope":null,"is_active":false},
       {"kind":"weekly_scoped","group":"weekly","percent":18,"severity":"normal",
        "resets_at":"2026-11-30T12:00:00.175809+00:00",
        "scope":{"model":{"id":null,"display_name":"Fable"},"surface":null},"is_active":true}],
     "seven_day_breakdown":{"as_of":"2026-11-24T12:31:43.189084+00:00",
        "window_started_at":"2026-11-23T12:00:00.175587+00:00","rows":[]},
     "weekly_scoped_shares":[{"limit_index":1,"used_percent_of_weekly":9,"allowance_percent_of_weekly":50}]}
    """;

    internal static readonly DateTimeOffset UltraNow = new(2026, 11, 24, 12, 31, 43, TimeSpan.Zero);

    // Formato antigo (sem "limits"): buckets fixos, nulos quando a cota não existe no plano.
    private const string Legacy = """
    {"five_hour":{"utilization":42.4,"resets_at":"2026-06-11T15:00:00Z"},
     "seven_day":{"utilization":78.0,"resets_at":"2026-06-18T09:00:00Z"},
     "seven_day_oauth_apps":null,
     "seven_day_opus":null,
     "seven_day_sonnet":{"utilization":96.0,"resets_at":"2026-06-18T09:00:00Z"},
     "extra_usage":{"is_enabled":false,"monthly_limit":null,"used_credits":null,"utilization":null,"currency":null}}
    """;

    [Fact]
    public void Ultra_mostra_semana_e_fable_sem_sessao_de_5h()
    {
        var s = UsageResponseParser.Parse(Ultra, UltraNow, Brt);
        Assert.Equal(new[] { "Semana", "Fable" }, s.Meters.Select(m => m.Label));
        Assert.Equal(new[] { 12, 18 }, s.Meters.Select(m => m.Pct));
        Assert.Equal(new[] { "weekly_all", "weekly_scoped:fable" }, s.Meters.Select(m => m.Key));
        Assert.Equal(SnapshotState.Ok, s.State);
    }

    [Fact]
    public void Janela_semanal_vem_do_breakdown()
    {
        var s = UsageResponseParser.Parse(Ultra, UltraNow, Brt);
        var start = DateTimeOffset.Parse("2026-11-23T12:00:00.175587+00:00");
        Assert.All(s.Meters, m => Assert.Equal(start, m.WindowStart));
    }

    [Fact]
    public void Fable_no_ritmo_atual_acaba_antes_do_reset()
    {
        var s = UsageResponseParser.Parse(Ultra, UltraNow, Brt);
        var fable = s.Find("weekly_scoped:fable")!;
        Assert.True(fable.Warn);
        Assert.Equal("⚠ acaba dom 01:16", fable.Hint);

        var semana = s.Find("weekly_all")!;
        Assert.False(semana.Warn);
        Assert.Equal("↻ seg 09:00", semana.Hint);
    }

    [Fact]
    public void Fatia_do_fable_entra_no_medidor_da_semana()
    {
        var s = UsageResponseParser.Parse(Ultra, UltraNow, Brt);
        var semana = s.Find("weekly_all")!;
        Assert.Equal((9, 50, "Fable"), (semana.Part, semana.PartCap, semana.PartLabel));
        Assert.Equal("Fable 9 de 12%", semana.PartText);
        Assert.Null(s.Find("weekly_scoped:fable")!.Part);
    }

    [Fact]
    public void Breakdown_ignora_linhas_zeradas()
    {
        const string json = """
        {"limits":[{"kind":"weekly_all","group":"weekly","percent":12,"resets_at":"2026-11-30T12:00:00Z"}],
         "seven_day_breakdown":{"rows":[
           {"key":"claude_code","display_name":"Claude Code","percent":92},
           {"key":"chat","display_name":"Chats","percent":0},
           {"key":"cowork","display_name":"Cowork","percent":8}]}}
        """;
        var s = UsageResponseParser.Parse(json, UltraNow, Brt);
        Assert.Equal(new[] { "Claude Code", "Cowork" }, s.Breakdown.Select(b => b.Label));
        Assert.Equal("Semana: Claude Code 92% · Cowork 8%", s.BreakdownText);
    }

    [Fact]
    public void Critico_e_o_que_acaba_antes_do_reset_mesmo_com_percentual_menor()
    {
        var s = UsageResponseParser.Parse(Ultra, UltraNow, Brt);
        Assert.Equal("Fable", s.Critical.Label);
        Assert.Equal(Zone.Verde, s.Critical.Zone);
        Assert.Equal(Zone.Ambar, s.Critical.DisplayZone); // 18% verde, mas acaba antes do reset
        Assert.Equal(Zone.Verde, s.Find("weekly_all")!.DisplayZone);
    }

    [Fact]
    public void Sessao_de_5h_vem_primeiro_quando_existe()
    {
        const string json = """
        {"limits":[
          {"kind":"weekly_all","group":"weekly","percent":30,"resets_at":"2026-11-30T12:00:00Z"},
          {"kind":"five_hour","group":"session","percent":55,"resets_at":"2026-11-24T15:00:00Z"}]}
        """;
        var s = UsageResponseParser.Parse(json, UltraNow, Brt);
        Assert.Equal(new[] { "Sessão 5h", "Semana" }, s.Meters.Select(m => m.Label));
        Assert.Equal(new DateTimeOffset(2026, 11, 24, 10, 0, 0, TimeSpan.Zero), s.Meters[0].WindowStart);
    }

    [Fact]
    public void Limite_desconhecido_aparece_com_o_nome_do_kind()
    {
        const string json = """{"limits":[{"kind":"monthly_all","group":"monthly","percent":7,"resets_at":null}]}""";
        var m = UsageResponseParser.Parse(json, UltraNow, Brt).Meters.Single();
        Assert.Equal("Monthly all", m.Label);
        Assert.Equal(7, m.Pct);
        Assert.Null(m.Hint);
    }

    [Fact]
    public void Sem_limits_usa_os_buckets_antigos()
    {
        var s = UsageResponseParser.Parse(Legacy, DateTimeOffset.UnixEpoch, TimeZoneInfo.Utc);
        Assert.Equal(new[] { "Sessão 5h", "Semana", "Sonnet" }, s.Meters.Select(m => m.Label));
        Assert.Equal(new[] { 42, 78, 96 }, s.Meters.Select(m => m.Pct));
        Assert.NotNull(s.Meters[0].ResetAt);
    }

    [Fact]
    public void Bucket_nulo_some_em_vez_de_virar_zero()
    {
        const string json = """
        {"five_hour":null,
         "seven_day":{"utilization":20.0,"resets_at":"2026-06-18T09:00:00Z"},
         "seven_day_sonnet":{"utilization":null,"resets_at":null}}
        """;
        var s = UsageResponseParser.Parse(json, DateTimeOffset.UnixEpoch, TimeZoneInfo.Utc);
        Assert.Equal(new[] { "Semana", "Sonnet" }, s.Meters.Select(m => m.Label));
        Assert.Equal(0, s.Find("seven_day_sonnet")!.Pct);
        Assert.Null(s.Find("seven_day_sonnet")!.ResetAt);
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
        var handler = new FakeHttpHandler(HttpStatusCode.OK, Ultra);
        var s = await new UsageClient(new HttpClient(handler)).FetchAsync("AT", default);
        Assert.Equal("Bearer", handler.LastRequest!.Headers.Authorization!.Scheme);
        Assert.Equal("oauth-2025-04-20", handler.LastRequest!.Headers.GetValues("anthropic-beta").Single());
        Assert.Equal(18, s.Find("weekly_scoped:fable")!.Pct);
    }
}
