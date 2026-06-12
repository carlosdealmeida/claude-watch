namespace ClaudeWatch.Infrastructure;

public static class AppPaths
{
    public static string BaseDir { get; } = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "ClaudeWatch");
    public static string LogsDir { get; } = Path.Combine(BaseDir, "logs");
}
