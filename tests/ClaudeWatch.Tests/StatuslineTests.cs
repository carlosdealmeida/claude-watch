using System.Text.Json.Nodes;
using ClaudeWatch.Core;
using ClaudeWatch.Infrastructure;
using Xunit;

public class StatuslineTests
{
    private static readonly DateTimeOffset Now = new(2026, 11, 24, 12, 31, 43, TimeSpan.Zero);
    private static readonly long ResetWeek = new DateTimeOffset(2026, 11, 30, 12, 0, 0, TimeSpan.Zero).ToUnixTimeSeconds();
    private static readonly long ResetSession = new DateTimeOffset(2026, 11, 24, 14, 0, 0, TimeSpan.Zero).ToUnixTimeSeconds();

    // O JSON que o Claude Code passa no stdin da statusline (recortado). rate_limits segue
    // https://code.claude.com/docs/en/statusline — cada janela pode faltar.
    private static string Stdin(string rateLimits) => J("""
    {"session_id":"abc","cwd":"C:/projetos/segredo","model":{"display_name":"Fable 5.2"},
     "rate_limits":RATE_LIMITS}
    """).Replace("RATE_LIMITS", J(rateLimits));

    /// <summary>Troca os marcadores pelos epochs (interpolação em raw string brigaria com as chaves do JSON).</summary>
    private static string J(string json) =>
        json.Replace("RESET_WEEK", ResetWeek.ToString()).Replace("RESET_SESSION", ResetSession.ToString());

    private static string TempFile() => Path.Combine(Path.GetTempPath(), "cw-" + Guid.NewGuid(), "statusline.json");

    [Fact]
    public void Parser_le_janelas_em_epoch()
    {
        var json = J("""
        {"captured_at":"2026-11-24T12:30:00+00:00",
         "rate_limits":{"five_hour":{"used_percentage":23.5,"resets_at":RESET_SESSION},
                        "seven_day":{"used_percentage":41.2,"resets_at":RESET_WEEK}}}
        """);
        var s = StatuslineParser.Parse(json, Now, TimeZoneInfo.Utc);
        Assert.Equal(new[] { "Sessão 5h", "Semana" }, s.Meters.Select(m => m.Label));
        Assert.Equal(new[] { 24, 41 }, s.Meters.Select(m => m.Pct));
        Assert.Equal(DateTimeOffset.FromUnixTimeSeconds(ResetWeek) - TimeSpan.FromDays(7), s.Meters[1].WindowStart);
        Assert.Equal(new DateTimeOffset(2026, 11, 24, 12, 30, 0, TimeSpan.Zero), s.CollectedAt);
    }

    [Fact]
    public void Ultra_sem_sessao_mostra_so_a_semana()
    {
        var json = J("""{"rate_limits":{"seven_day":{"used_percentage":12,"resets_at":RESET_WEEK}}}""");
        var s = StatuslineParser.Parse(json, Now, TimeZoneInfo.Utc);
        Assert.Equal("Semana", s.Meters.Single().Label);
    }

    [Fact]
    public void Janela_que_ja_virou_recomeca_do_zero()
    {
        var json = J("""{"rate_limits":{"five_hour":{"used_percentage":90,"resets_at":RESET_SESSION}}}""");
        var m = StatuslineParser.Parse(json, Now.AddHours(3), TimeZoneInfo.Utc).Meters.Single();
        Assert.Equal(0, m.Pct);
        Assert.Null(m.ResetAt);
    }

    [Fact]
    public void Ponte_grava_so_os_limites()
    {
        var path = TempFile();
        StatuslineBridge.Capture(Stdin("""{"seven_day":{"used_percentage":12,"resets_at":RESET_WEEK}}"""), path, Now, print: false);
        var saved = JsonNode.Parse(File.ReadAllText(path))!.AsObject();
        Assert.Equal(new[] { "captured_at", "rate_limits" }, saved.Select(p => p.Key).OrderBy(k => k));
        Assert.DoesNotContain("segredo", File.ReadAllText(path));
    }

    [Fact]
    public void Ponte_nao_apaga_o_ultimo_dado_quando_a_sessao_ainda_nao_tem_limites()
    {
        var path = TempFile();
        StatuslineBridge.Capture(Stdin("""{"seven_day":{"used_percentage":12,"resets_at":RESET_WEEK}}"""), path, Now, print: false);
        var antes = File.ReadAllText(path);
        var line = StatuslineBridge.Capture("""{"session_id":"nova","cwd":"x"}""", path, Now.AddMinutes(5), print: true);
        Assert.Equal(antes, File.ReadAllText(path));
        Assert.Equal("ClaudeWatch · aguardando a primeira resposta", line);
    }

    [Fact]
    public void Ponte_imprime_a_linha_dos_limites()
    {
        var line = StatuslineBridge.Capture(
            Stdin("""{"seven_day":{"used_percentage":12,"resets_at":RESET_WEEK}}"""), TempFile(), Now, print: true);
        Assert.StartsWith("Sem 12%", line);
    }

    [Fact]
    public void Ponte_nunca_lanca_com_json_invalido()
    {
        Assert.Null(StatuslineBridge.Capture("isso não é json", TempFile(), Now, print: false));
        Assert.Equal("ClaudeWatch · aguardando a primeira resposta",
            StatuslineBridge.Capture("", TempFile(), Now, print: true));
    }
}

public class ClaudeStatuslineSetupTests
{
    private const string Exe = @"C:\Users\carlos\Apps\ClaudeWatch.exe";

    private static string Settings(string? content)
    {
        var dir = Directory.CreateDirectory(Path.Combine(Path.GetTempPath(), "cw-" + Guid.NewGuid(), ".claude")).FullName;
        var path = Path.Combine(dir, "settings.json");
        if (content is not null) File.WriteAllText(path, content);
        return path;
    }

    [Fact]
    public void Comando_usa_barras_normais_entre_aspas() =>
        Assert.Equal("\"C:/Users/carlos/Apps/ClaudeWatch.exe\" --statusline", ClaudeStatuslineSetup.Command(Exe));

    [Fact]
    public void Sem_arquivo_cria_so_a_statusline()
    {
        var path = Settings(null);
        Assert.Equal(StatuslineState.None, ClaudeStatuslineSetup.Inspect(path).State);
        ClaudeStatuslineSetup.Install(path, Exe);
        var (state, cmd) = ClaudeStatuslineSetup.Inspect(path);
        Assert.Equal(StatuslineState.Ours, state);
        Assert.Equal(ClaudeStatuslineSetup.Command(Exe), cmd);
    }

    [Fact]
    public void Instalar_preserva_o_resto_e_guarda_backup()
    {
        var path = Settings("""
        {
          // comentário que o Claude Code aceita
          "model": "fable",
          "permissions": { "allow": ["Bash(git:*)"] },
        }
        """);
        ClaudeStatuslineSetup.Install(path, Exe);
        var root = JsonNode.Parse(File.ReadAllText(path))!;
        Assert.Equal("fable", (string)root["model"]!);
        Assert.Equal("Bash(git:*)", (string)root["permissions"]!["allow"]![0]!);
        Assert.Equal("command", (string)root["statusLine"]!["type"]!);
        Assert.True(File.Exists(path + ".claudewatch.bak"));
    }

    [Fact]
    public void Instalar_nao_escapa_o_texto_do_resto_do_arquivo()
    {
        // Quem abrir o settings.json depois tem de reconhecer o que escreveu: && e acentos
        // continuam literais, não viram & nem ç.
        var path = Settings("""
        {
          "hooks": { "Stop": [{ "hooks": [{ "type": "command", "command": "git add -A && echo ação > log.txt" }] }] }
        }
        """);
        ClaudeStatuslineSetup.Install(path, Exe);
        var texto = File.ReadAllText(path);
        Assert.Contains("git add -A && echo ação > log.txt", texto);
    }

    [Theory]
    // Ao abrir o app (rotina): só acompanha o .exe que mudou de pasta. Statusline ausente
    // fica ausente — a pessoa pode tê-la tirado de propósito.
    [InlineData(StatuslineState.None, null, true, StatuslineAction.Nothing)]
    [InlineData(StatuslineState.Ours, "\"C:/velho/ClaudeWatch.exe\" --statusline", true, StatuslineAction.UpdatePath)]
    [InlineData(StatuslineState.Ours, "\"C:/Users/carlos/Apps/ClaudeWatch.exe\" --statusline", true, StatuslineAction.Nothing)]
    [InlineData(StatuslineState.Other, "meu.sh", true, StatuslineAction.Nothing)]
    [InlineData(StatuslineState.Unreadable, null, true, StatuslineAction.Nothing)]
    // Pedido explícito (diálogo ou menu): instala, ou explica por que não.
    [InlineData(StatuslineState.None, null, false, StatuslineAction.Install)]
    [InlineData(StatuslineState.Ours, "\"C:/velho/ClaudeWatch.exe\" --statusline", false, StatuslineAction.UpdatePath)]
    [InlineData(StatuslineState.Ours, "\"C:/Users/carlos/Apps/ClaudeWatch.exe\" --statusline", false, StatuslineAction.AlreadyOurs)]
    [InlineData(StatuslineState.Other, "meu.sh", false, StatuslineAction.ExplainOther)]
    [InlineData(StatuslineState.Unreadable, null, false, StatuslineAction.ExplainUnreadable)]
    public void Plano_da_statusline(StatuslineState state, string? command, bool routine, StatuslineAction esperado) =>
        Assert.Equal(esperado, ClaudeStatuslineSetup.Plan(state, command, Exe, routine));

    [Fact]
    public void Nunca_substitui_a_statusline_de_outra_ferramenta()
    {
        var original = """{"statusLine":{"type":"command","command":"~/.claude/statusline.sh"}}""";
        var path = Settings(original);
        var (state, cmd) = ClaudeStatuslineSetup.Inspect(path);
        Assert.Equal((StatuslineState.Other, "~/.claude/statusline.sh"), (state, cmd));
        Assert.Throws<InvalidOperationException>(() => ClaudeStatuslineSetup.Install(path, Exe));
        Assert.Equal(original, File.ReadAllText(path));
    }

    [Fact]
    public void Script_que_so_captura_conta_como_de_outra_pessoa() =>
        Assert.False(ClaudeStatuslineSetup.IsOurs(
            "bash -c 'input=$(cat); printf %s \"$input\" | ClaudeWatch.exe --statusline-capture; meu.sh'"));

    [Fact]
    public void Arquivo_ilegivel_nao_e_tocado()
    {
        var path = Settings("{ isso não é json");
        Assert.Equal(StatuslineState.Unreadable, ClaudeStatuslineSetup.Inspect(path).State);
        Assert.Throws<InvalidOperationException>(() => ClaudeStatuslineSetup.Install(path, Exe));
        Assert.Equal("{ isso não é json", File.ReadAllText(path));
    }

    [Fact]
    public void Desinstalar_remove_so_a_nossa()
    {
        var path = Settings("""{"model":"fable"}""");
        ClaudeStatuslineSetup.Install(path, Exe);
        Assert.True(ClaudeStatuslineSetup.Uninstall(path));
        var root = JsonNode.Parse(File.ReadAllText(path))!.AsObject();
        Assert.False(root.ContainsKey("statusLine"));
        Assert.Equal("fable", (string)root["model"]!);

        var outra = Settings("""{"statusLine":{"type":"command","command":"meu.sh"}}""");
        Assert.False(ClaudeStatuslineSetup.Uninstall(outra));
    }
}
