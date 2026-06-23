using ClaudeWatch.Core;

namespace ClaudeWatch.Credentials;

/// <summary>
/// Pipeline SOMENTE-LEITURA da credencial do Claude Code.
///
/// O ClaudeWatch e o Claude Code compartilham o MESMO refresh token do arquivo
/// <c>.credentials.json</c>. O endpoint OAuth da Anthropic ROTACIONA o refresh token
/// a cada renovação (invalida o antigo). Se o ClaudeWatch renovasse, ele consumiria/
/// rotacionaria esse token e o Claude Code seria deslogado no dia seguinte.
///
/// Por isso este pipeline NUNCA renova: usa apenas o access token que já está no
/// arquivo (o próprio Claude Code o mantém fresco quando em uso). Se o access token
/// venceu e o CLI está ocioso, reportamos <see cref="SnapshotState.Stale"/> e esperamos
/// o CLI renovar sozinho — jamais tocamos no refresh token.
/// </summary>
public sealed class CredentialPipeline(ICredentialFile file)
{
    // Pequena margem para evitar disparar uma chamada com um token que vence em segundos.
    private static readonly TimeSpan Margin = TimeSpan.FromMinutes(1);

    public Task<TokenResult> GetAccessTokenAsync(DateTimeOffset now, CancellationToken ct) =>
        Task.FromResult(Resolve(now));

    public TokenResult Resolve(DateTimeOffset now)
    {
        var cred = ParseFile();
        if (cred?.AccessToken is not { Length: > 0 } token)
            return new(null, SnapshotState.NoCredential);
        if (cred.ExpiresAt <= now + Margin)
            return new(null, SnapshotState.Stale);
        return new(token, SnapshotState.Ok);
    }

    private OAuthCredential? ParseFile() =>
        file.ReadOrNull() is { } j ? ClaudeCodeCredentialsParser.TryParse(j) : null;
}
