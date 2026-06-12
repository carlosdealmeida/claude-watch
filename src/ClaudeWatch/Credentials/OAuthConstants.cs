namespace ClaudeWatch.Credentials;

/// <summary>
/// Valores canônicos do Claude Code CLI v2.1.88, extraídos do binário oficial
/// (via claude-code-dotnet/CORTEX e o port em ATLAS). O endpoint de usage é o mesmo
/// que alimenta o comando `/usage` do CLI: autentica só com o Bearer OAuth, sem cookies.
/// </summary>
public static class OAuthConstants
{
    public const string ClientId = "9d1c250a-e61b-44d9-88ed-5944d1962f5e";
    public const string TokenEndpoint = "https://platform.claude.com/v1/oauth/token";
    public const string UsageEndpoint = "https://api.anthropic.com/api/oauth/usage";

    public const string BetaHeader = "oauth-2025-04-20";
    public const string ApiVersion = "2023-06-01";
    public const string UserAgent = "claude-cli/2.1.90 (external, cli)";
}
