using System.Security.Cryptography;
using System.Text.Json;

namespace ClaudeWatch.Credentials;

public sealed class TokenCache(string baseDir)
{
    private readonly string _path = Path.Combine(baseDir, "token.bin");
    private sealed record CachedToken(string AccessToken, DateTimeOffset ExpiresAt);

    public void Save(OAuthCredential cred)
    {
        Directory.CreateDirectory(baseDir);
        var payload = JsonSerializer.SerializeToUtf8Bytes(new CachedToken(cred.AccessToken, cred.ExpiresAt));
        File.WriteAllBytes(_path, ProtectedData.Protect(payload, null, DataProtectionScope.CurrentUser));
    }

    public OAuthCredential? Load()
    {
        try
        {
            if (!File.Exists(_path)) return null;
            var raw = ProtectedData.Unprotect(File.ReadAllBytes(_path), null, DataProtectionScope.CurrentUser);
            var t = JsonSerializer.Deserialize<CachedToken>(raw);
            return t is null ? null : new OAuthCredential(t.AccessToken, null, t.ExpiresAt);
        }
        catch { return null; }
    }

    public void Clear() { try { File.Delete(_path); } catch { /* best effort */ } }
}
