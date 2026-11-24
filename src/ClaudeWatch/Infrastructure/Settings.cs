using System.Text.Json;

namespace ClaudeWatch.Infrastructure;

public sealed record Settings
{
    public double? PosX { get; init; }
    public double? PosY { get; init; }
    public string Skin { get; init; } = "Aneis"; // "Aneis" | "Barras" | "Compacto" | "Led"
    public bool Locked { get; init; }
    public int IntervalSeconds { get; init; } = 60;
    public string? WslCredentialsPath { get; init; }

    /// <summary>
    /// De onde vêm os limites: <c>"statusline"</c> (oficial, pela statusline do Claude Code)
    /// ou <c>"api"</c> (endpoint não oficial, com o limite do Fable). Nulo: ainda não escolhido.
    /// </summary>
    public string? Source { get; init; }
}

public static class Sources
{
    public const string Statusline = "statusline";
    public const string Api = "api";
}

public sealed class SettingsStore(string baseDir)
{
    private readonly string _path = Path.Combine(baseDir, "settings.json");
    private static readonly JsonSerializerOptions Opt = new() { WriteIndented = true };

    public Settings Load()
    {
        try
        {
            return File.Exists(_path)
                ? JsonSerializer.Deserialize<Settings>(File.ReadAllText(_path), Opt) ?? new Settings()
                : new Settings();
        }
        catch { return new Settings(); }
    }

    public void Save(Settings s)
    {
        Directory.CreateDirectory(baseDir);
        File.WriteAllText(_path, JsonSerializer.Serialize(s, Opt));
    }
}
