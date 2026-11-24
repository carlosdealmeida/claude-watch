using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace ClaudeWatch.Infrastructure;

public enum StatuslineState
{
    /// <summary>Nenhuma statusline configurada no Claude Code.</summary>
    None,
    /// <summary>A statusline é a do ClaudeWatch.</summary>
    Ours,
    /// <summary>Há outra statusline; o ClaudeWatch não a substitui.</summary>
    Other,
    /// <summary>O settings.json existe mas não é um JSON legível; não mexemos nele.</summary>
    Unreadable,
}

/// <summary>O que fazer com a statusline do Claude Code, decidido por <see cref="ClaudeStatuslineSetup.Plan"/>.</summary>
public enum StatuslineAction
{
    Nothing,
    Install,
    /// <summary>A statusline é a nossa, mas aponta para um .exe em outra pasta.</summary>
    UpdatePath,
    AlreadyOurs,
    ExplainOther,
    ExplainUnreadable,
}

/// <summary>
/// Configura o ClaudeWatch como statusline do Claude Code em <c>~/.claude/settings.json</c>,
/// preservando tudo o mais que estiver no arquivo e guardando um backup antes da primeira mudança.
/// Nunca substitui uma statusline de outra pessoa ou ferramenta.
/// </summary>
public static class ClaudeStatuslineSetup
{
    private static readonly JsonDocumentOptions ReadOpt = new()
    { CommentHandling = JsonCommentHandling.Skip, AllowTrailingCommas = true };
    // O arquivo é da pessoa: && , > e acentos continuam literais, não viram & nem ç.
    private static readonly JsonSerializerOptions WriteOpt = new()
    { WriteIndented = true, Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping };

    public static string SettingsPath(string home) => Path.Combine(home, ".claude", "settings.json");

    /// <summary>Barras normais no caminho: funciona igual no cmd, no PowerShell e no Git Bash.</summary>
    public static string Command(string exePath) => $"\"{exePath.Replace('\\', '/')}\" {StatuslineBridge.Arg}";

    /// <summary>Linha para colocar no começo de um script de statusline em bash que já existe.</summary>
    public static string BashSnippet(string exePath) =>
        $"input=$(cat); printf '%s' \"$input\" | \"{exePath.Replace('\\', '/')}\" {StatuslineBridge.CaptureArg}";

    public static (StatuslineState State, string? Command) Inspect(string settingsPath)
    {
        if (!File.Exists(settingsPath)) return (StatuslineState.None, null);
        JsonNode? root;
        try { root = JsonNode.Parse(File.ReadAllText(settingsPath), documentOptions: ReadOpt); }
        catch { return (StatuslineState.Unreadable, null); }

        var cmd = root?["statusLine"]?["command"] is JsonValue v && v.TryGetValue<string>(out var c) ? c : null;
        if (string.IsNullOrWhiteSpace(cmd)) return (StatuslineState.None, null);
        return IsOurs(cmd) ? (StatuslineState.Ours, cmd) : (StatuslineState.Other, cmd);
    }

    /// <summary>
    /// Decide o que fazer. Na <paramref name="routine"/> (o app abrindo) só acompanha o .exe que
    /// mudou de pasta: uma statusline ausente fica ausente, porque a pessoa pode tê-la tirado de
    /// propósito. Instalar ou explicar só quando ela pede (diálogo da fonte ou menu do tray).
    /// </summary>
    public static StatuslineAction Plan(StatuslineState state, string? command, string exePath, bool routine) =>
        state switch
        {
            StatuslineState.Ours when command != Command(exePath) => StatuslineAction.UpdatePath,
            _ when routine => StatuslineAction.Nothing,
            StatuslineState.None => StatuslineAction.Install,
            StatuslineState.Ours => StatuslineAction.AlreadyOurs,
            StatuslineState.Other => StatuslineAction.ExplainOther,
            _ => StatuslineAction.ExplainUnreadable,
        };

    public static bool IsOurs(string command) =>
        command.Contains("ClaudeWatch", StringComparison.OrdinalIgnoreCase)
        && command.Contains(StatuslineBridge.Arg, StringComparison.Ordinal)
        && !command.Contains(StatuslineBridge.CaptureArg, StringComparison.Ordinal);

    /// <summary>
    /// Grava <c>statusLine</c> apontando para este executável. Recusa quando há outra statusline
    /// ou quando o arquivo não é legível — quem decide nesses casos é a pessoa.
    /// </summary>
    public static void Install(string settingsPath, string exePath)
    {
        var (state, _) = Inspect(settingsPath);
        if (state is StatuslineState.Other or StatuslineState.Unreadable)
            throw new InvalidOperationException($"statusLine não pode ser alterada automaticamente ({state})");

        JsonObject root = File.Exists(settingsPath)
            ? JsonNode.Parse(File.ReadAllText(settingsPath), documentOptions: ReadOpt) as JsonObject ?? new JsonObject()
            : new JsonObject();

        var backup = settingsPath + ".claudewatch.bak";
        if (File.Exists(settingsPath) && !File.Exists(backup)) File.Copy(settingsPath, backup);

        root["statusLine"] = new JsonObject { ["type"] = "command", ["command"] = Command(exePath) };
        Directory.CreateDirectory(Path.GetDirectoryName(settingsPath)!);
        File.WriteAllText(settingsPath, root.ToJsonString(WriteOpt));
    }

    /// <summary>Remove a statusline do ClaudeWatch (e só ela).</summary>
    public static bool Uninstall(string settingsPath)
    {
        if (Inspect(settingsPath).State != StatuslineState.Ours) return false;
        var root = (JsonObject)JsonNode.Parse(File.ReadAllText(settingsPath), documentOptions: ReadOpt)!;
        root.Remove("statusLine");
        File.WriteAllText(settingsPath, root.ToJsonString(WriteOpt));
        return true;
    }
}
