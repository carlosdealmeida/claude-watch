using ClaudeWatch.Infrastructure;
using Xunit;

public class SettingsStoreTests
{
    private static string TempDir() => Directory.CreateDirectory(
        Path.Combine(Path.GetTempPath(), "cw-" + Guid.NewGuid())).FullName;

    [Fact]
    public void Ausente_retorna_defaults()
    {
        var s = new SettingsStore(TempDir()).Load();
        Assert.Equal("Aneis", s.Skin);
        Assert.Equal(60, s.IntervalSeconds);
        Assert.False(s.Locked);
    }

    [Fact]
    public void Round_trip()
    {
        var dir = TempDir(); var store = new SettingsStore(dir);
        store.Save(new Settings { PosX = 10, PosY = 20, Skin = "Led", Locked = true });
        var s = store.Load();
        Assert.Equal((10d, 20d, "Led", true), (s.PosX!.Value, s.PosY!.Value, s.Skin, s.Locked));
    }

    [Fact]
    public void Corrompido_degrada_para_defaults()
    {
        var dir = TempDir();
        File.WriteAllText(Path.Combine(dir, "settings.json"), "{nope");
        Assert.Equal("Aneis", new SettingsStore(dir).Load().Skin);
    }
}
