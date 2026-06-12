namespace ClaudeWatch.Credentials;

public static class TokenSelector
{
    public static OAuthCredential? PickValid(
        OAuthCredential? file, OAuthCredential? cache, DateTimeOffset now, TimeSpan margin) =>
        new[] { file, cache }
            .Where(c => c is not null && c.ExpiresAt > now + margin)
            .OrderByDescending(c => c!.ExpiresAt)
            .FirstOrDefault();
}
