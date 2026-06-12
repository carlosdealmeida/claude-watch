using System.Text.Json.Nodes;

namespace ClaudeWatch.Credentials;

public sealed record RefreshResult(OAuthCredential? Credential, bool Rejected, bool RotationDetected);

public sealed class OAuthRefreshClient(HttpClient http)
{
    public async Task<RefreshResult> RefreshAsync(string refreshToken, CancellationToken ct)
    {
        // O endpoint OAuth da Anthropic espera application/x-www-form-urlencoded
        // (igual ao Claude Code CLI / CORTEX). Scopes omitidos de propósito.
        using var req = new HttpRequestMessage(HttpMethod.Post, OAuthConstants.TokenEndpoint)
        {
            Content = new FormUrlEncodedContent(new Dictionary<string, string>
            {
                ["grant_type"] = "refresh_token",
                ["refresh_token"] = refreshToken,
                ["client_id"] = OAuthConstants.ClientId,
            }),
        };
        req.Headers.TryAddWithoutValidation("User-Agent", OAuthConstants.UserAgent);

        using var resp = await http.SendAsync(req, ct);
        if ((int)resp.StatusCode is >= 400 and < 500) return new(null, Rejected: true, false);
        resp.EnsureSuccessStatusCode();

        var node = JsonNode.Parse(await resp.Content.ReadAsStringAsync(ct))!;
        var access = node["access_token"]!.GetValue<string>();
        var expiresIn = node["expires_in"]?.GetValue<int>() ?? 3600;
        var newRefresh = node["refresh_token"]?.GetValue<string>();

        var cred = new OAuthCredential(access, refreshToken, DateTimeOffset.UtcNow.AddSeconds(expiresIn));
        return new(cred, false, RotationDetected: newRefresh is not null && newRefresh != refreshToken);
    }
}
