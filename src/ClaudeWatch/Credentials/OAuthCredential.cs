namespace ClaudeWatch.Credentials;

public sealed record OAuthCredential(string AccessToken, string? RefreshToken, DateTimeOffset ExpiresAt);
