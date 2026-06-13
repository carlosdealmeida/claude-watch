namespace ClaudeWatch.Core;

public sealed record UpdateStatus(bool Available, string? LatestVersion, string? Url)
{
    public static readonly UpdateStatus None = new(false, null, null);
}

public static class UpdateChecker
{
    public static UpdateStatus Check(Version current, string? latestTag, string? url)
    {
        if (string.IsNullOrWhiteSpace(latestTag) || string.IsNullOrWhiteSpace(url))
            return UpdateStatus.None;

        var cleaned = latestTag.Trim().TrimStart('v', 'V');
        if (!Version.TryParse(cleaned, out var latest))
            return UpdateStatus.None;

        return Normalize(latest) > Normalize(current)
            ? new UpdateStatus(true, cleaned, url)
            : UpdateStatus.None;
    }

    // Ignora o componente Revision e trata Build ausente (-1) como 0,
    // para comparar "0.2" e "0.1.0.0" de forma consistente.
    private static Version Normalize(Version v) =>
        new(v.Major, v.Minor, v.Build < 0 ? 0 : v.Build);
}
