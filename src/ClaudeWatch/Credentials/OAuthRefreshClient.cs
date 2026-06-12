using System.Net.Http.Json;
using System.Text.Json.Nodes;

namespace ClaudeWatch.Credentials;

public sealed record RefreshResult(OAuthCredential? Credential, bool Rejected, bool RotationDetected);

public sealed class OAuthRefreshClient(HttpClient http)
{
    public async Task<RefreshResult> RefreshAsync(string refreshToken, CancellationToken ct)
    {
        // TODO(carlos): confirmar se o grant é JSON ou form-urlencoded no parity.
        using var resp = await http.PostAsJsonAsync(OAuthConstants.TokenEndpoint,
            new { grant_type = "refresh_token", refresh_token = refreshToken, client_id = OAuthConstants.ClientId }, ct);

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
