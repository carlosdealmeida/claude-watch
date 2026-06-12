namespace ClaudeWatch.Credentials;

public sealed class CredentialPipeline(
    ICredentialFile file, TokenCache cache, OAuthRefreshClient refresh, Action<string> log)
{
    private static readonly TimeSpan Margin = TimeSpan.FromMinutes(2);
    private readonly SemaphoreSlim _gate = new(1, 1);
    public bool NoCredential { get; private set; }

    public async Task<string?> GetAccessTokenAsync(DateTimeOffset now, CancellationToken ct)
    {
        if (Pick(now) is { } fast) { NoCredential = false; return fast.AccessToken; }

        await _gate.WaitAsync(ct);
        try
        {
            // re-read: alguém pode ter renovado enquanto esperávamos o gate
            if (Pick(now) is { } again) { NoCredential = false; return again.AccessToken; }

            var fileCred = ParseFile();
            if (fileCred?.RefreshToken is not { } rt) { NoCredential = true; return null; }

            var result = await refresh.RefreshAsync(rt, ct);
            if (result.Rejected) { cache.Clear(); NoCredential = true; return null; }
            if (result.RotationDetected)
                log("AVISO: refresh_token rotacionou; modo degradado — NUNCA escrever no arquivo do Claude Code.");
            if (result.Credential is { } c) { cache.Save(c); NoCredential = false; return c.AccessToken; }
            return null;
        }
        finally { _gate.Release(); }
    }

    private OAuthCredential? Pick(DateTimeOffset now) =>
        TokenSelector.PickValid(ParseFile(), cache.Load(), now, Margin);

    private OAuthCredential? ParseFile() =>
        file.ReadOrNull() is { } j ? ClaudeCodeCredentialsParser.TryParse(j) : null;
}
