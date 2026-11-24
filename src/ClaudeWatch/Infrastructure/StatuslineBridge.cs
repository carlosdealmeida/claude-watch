using System.Text;
using System.Text.Json.Nodes;
using ClaudeWatch.Core;

namespace ClaudeWatch.Infrastructure;

/// <summary>
/// A ponte entre a statusline do Claude Code e o widget.
///
/// O Claude Code chama o comando da statusline a cada atualização, passando um JSON no stdin
/// que inclui <c>rate_limits</c> para assinantes. Rodando como esse comando
/// (<c>ClaudeWatch.exe --statusline</c>), o ClaudeWatch grava só os limites num arquivo local,
/// que o widget lê. Não toca no token e não chama nenhuma API: é o caminho oficial.
///
/// <c>--statusline</c> também imprime uma linha com os limites, para ser a statusline em si.
/// <c>--statusline-capture</c> só grava, sem imprimir: para encadear numa statusline que já existe.
/// </summary>
public static class StatuslineBridge
{
    public const string Arg = "--statusline";
    public const string CaptureArg = "--statusline-capture";

    public static string DataPath => Path.Combine(AppPaths.BaseDir, "statusline.json");

    /// <summary>
    /// Processa o JSON recebido da statusline. Grava o arquivo só quando ele traz
    /// <c>rate_limits</c> (o Claude Code só manda depois da primeira resposta da sessão),
    /// para que uma sessão recém-aberta não apague o último dado bom.
    /// Guarda apenas os limites: diretório, sessão e o resto do JSON ficam de fora.
    /// </summary>
    /// <returns>A linha a imprimir, ou nulo quando <paramref name="print"/> é falso.</returns>
    public static string? Capture(string stdinJson, string dataPath, DateTimeOffset now, bool print)
    {
        JsonObject? saved = null;
        try
        {
            if (JsonNode.Parse(stdinJson)?["rate_limits"] is JsonObject rl)
            {
                saved = new JsonObject
                {
                    ["captured_at"] = now.ToString("O"),
                    ["rate_limits"] = rl.DeepClone(),
                };
                WriteAtomically(dataPath, saved.ToJsonString());
            }
        }
        catch
        {
            // JSON inválido ou disco indisponível: a statusline nunca pode quebrar o Claude Code.
        }

        if (!print) return null;
        if (saved is null) return "ClaudeWatch · aguardando a primeira resposta";
        var line = TooltipFormatter.Format(StatuslineParser.Parse(saved.ToJsonString(), now));
        return line == "ClaudeWatch" ? "ClaudeWatch · sem limites nesta conta" : line;
    }

    /// <summary>Lê o stdin inteiro e escreve a linha no stdout, em UTF-8. Nunca lança.</summary>
    public static int Run(bool print)
    {
        try
        {
            using var input = new StreamReader(Console.OpenStandardInput(), new UTF8Encoding(false));
            var line = Capture(input.ReadToEnd(), DataPath, DateTimeOffset.Now, print);
            if (line is not null)
            {
                using var output = Console.OpenStandardOutput();
                var bytes = new UTF8Encoding(false).GetBytes(line + "\n");
                output.Write(bytes, 0, bytes.Length);
            }
        }
        catch { /* sem stdin/stdout: nada a fazer */ }
        return 0;
    }

    private static void WriteAtomically(string path, string content)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        var tmp = path + ".tmp";
        File.WriteAllText(tmp, content, new UTF8Encoding(false));
        File.Move(tmp, path, overwrite: true);
    }
}
