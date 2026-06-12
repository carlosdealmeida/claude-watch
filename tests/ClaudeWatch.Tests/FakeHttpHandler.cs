using System.Net;

public sealed class FakeHttpHandler(HttpStatusCode code, string body) : HttpMessageHandler
{
    public HttpRequestMessage? LastRequest;
    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage r, CancellationToken ct)
    { LastRequest = r; return Task.FromResult(new HttpResponseMessage(code) { Content = new StringContent(body) }); }
}
