using System.Text.Json;

namespace ClaudeWatch.Infrastructure;

public sealed record Settings
{
    public double? PosX { get; init; }
    public double? PosY { get; init; }
    public string Skin { get; init; } = "Aneis"; // "Aneis" | "Led"
    public bool Locked { get; init; }
    public int IntervalSeconds { get; init; } = 60;
    public string? WslCredentialsPath { get; init; }
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
