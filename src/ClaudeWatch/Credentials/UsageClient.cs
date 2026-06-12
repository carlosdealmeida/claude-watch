using System.Net;
using ClaudeWatch.Core;

namespace ClaudeWatch.Credentials;

public sealed class UnauthorizedException : Exception;

public sealed class UsageClient(HttpClient http)
{
    public async Task<UsageSnapshot> FetchAsync(string accessToken, CancellationToken ct)
    {
        using var req = new HttpRequestMessage(HttpMethod.Get, OAuthConstants.UsageEndpoint);
        req.Headers.Authorization = new("Bearer", accessToken);
        using var resp = await http.SendAsync(req, ct);
        if (resp.StatusCode == HttpStatusCode.Unauthorized) throw new UnauthorizedException();
        resp.EnsureSuccessStatusCode();
        return UsageResponseParser.Parse(await resp.Content.ReadAsStringAsync(ct), DateTimeOffset.UtcNow);
    }
}
