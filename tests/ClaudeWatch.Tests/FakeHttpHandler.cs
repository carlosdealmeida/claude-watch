using System.Net;

public sealed class FakeHttpHandler(HttpStatusCode code, string body) : HttpMessageHandler
{
    public HttpRequestMessage? LastRequest;
    public string? LastBody;
    public string? LastContentType;

    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage r, CancellationToken ct)
    {
        LastRequest = r;
        LastContentType = r.Content?.Headers.ContentType?.MediaType;
        if (r.Content is not null) LastBody = await r.Content.ReadAsStringAsync(ct);
        return new HttpResponseMessage(code) { Content = new StringContent(body) };
    }
}
