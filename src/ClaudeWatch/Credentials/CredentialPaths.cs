using ClaudeWatch.Infrastructure;

namespace ClaudeWatch.Credentials;

public static class CredentialPaths
{
    public static string Resolve(string homeDir, Settings settings)
    {
        var primary = Path.Combine(homeDir, ".claude", ".credentials.json");
        if (File.Exists(primary)) return primary;
        return settings.WslCredentialsPath ?? primary;
    }
}
