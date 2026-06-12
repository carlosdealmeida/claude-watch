namespace ClaudeWatch.Credentials;

public interface ICredentialFile
{
    string? ReadOrNull();
    event Action? Changed;
}
