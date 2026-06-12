using System.Text.Json.Nodes;

namespace ClaudeWatch.Credentials;

public static class ClaudeCodeCredentialsParser
{
    // TODO(carlos): confirmar nomes de campos contra o parity do CORTEX.
    public static OAuthCredential? TryParse(string json)
    {
        try
        {
            var root = JsonNode.Parse(json);
            var o = root?["claudeAiOauth"] ?? root;
            var access = o?["accessToken"]?.GetValue<string>();
            if (string.IsNullOrEmpty(access)) return null;
            var refresh = o?["refreshToken"]?.GetValue<string>();
            var expMs = o?["expiresAt"]?.GetValue<long>() ?? 0;
            return new OAuthCredential(access, refresh, DateTimeOffset.FromUnixTimeMilliseconds(expMs));
        }
        catch { return null; }
    }
}
