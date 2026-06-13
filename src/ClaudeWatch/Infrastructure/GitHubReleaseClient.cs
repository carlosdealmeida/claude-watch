using System.Text.Json.Nodes;

namespace ClaudeWatch.Infrastructure;

public sealed record LatestRelease(string TagName, string HtmlUrl);

public sealed class GitHubReleaseClient(HttpClient http)
{
    public const string Endpoint =
        "https://api.github.com/repos/carlosdealmeida/claude-watch/releases/latest";

    public async Task<LatestRelease?> FetchLatestAsync(CancellationToken ct)
    {
        try
        {
            using var req = new HttpRequestMessage(HttpMethod.Get, Endpoint);
            req.Headers.TryAddWithoutValidation("User-Agent", "ClaudeWatch"); // GitHub exige UA
            req.Headers.TryAddWithoutValidation("Accept", "application/vnd.github+json");
            using var resp = await http.SendAsync(req, ct);
            if (!resp.IsSuccessStatusCode) return null;

            var node = JsonNode.Parse(await resp.Content.ReadAsStringAsync(ct));
            var tag = node?["tag_name"]?.GetValue<string>();
            var url = node?["html_url"]?.GetValue<string>();
            return tag is not null && url is not null ? new LatestRelease(tag, url) : null;
        }
        catch { return null; } // rede fora / JSON inesperado: sem update, nunca quebra
    }
}
