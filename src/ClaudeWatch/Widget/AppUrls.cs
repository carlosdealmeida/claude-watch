namespace ClaudeWatch.Widget;

public static class AppUrls
{
    // Página de login do Claude (mesma usada pelo fluxo OAuth do Claude Code).
    // O login efetivo do CLI também pode ser refeito com `claude` no terminal.
    public const string ClaudeCode = "https://claude.ai/login";

    // Como o ClaudeWatch lê os limites: as duas fontes, o formato dos dados e o cálculo do ritmo.
    public const string Docs = "https://github.com/carlosdealmeida/claude-watch/blob/master/LIMITES.md";
}
