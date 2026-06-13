using System.Reflection;

namespace ClaudeWatch.Infrastructure;

public static class AppVersion
{
    /// <summary>Versão do assembly em execução (vem de <Version> no csproj / -p:Version no publish).</summary>
    public static Version Current { get; } =
        Assembly.GetEntryAssembly()?.GetName().Version ?? new Version(0, 0, 0);
}
