namespace ClaudeWatch.Infrastructure;

public sealed class FileLogger(string dir)
{
    private readonly string _path = Path.Combine(dir, "claudewatch.log");
    private readonly Lock _lock = new();

    public void Log(string msg)
    {
        try
        {
            lock (_lock)
            {
                Directory.CreateDirectory(dir);
                if (File.Exists(_path) && new FileInfo(_path).Length > 1_000_000)
                    File.Move(_path, _path + ".1", overwrite: true);
                File.AppendAllText(_path, $"{DateTimeOffset.Now:O} {msg}{Environment.NewLine}");
            }
        }
        catch { /* logging nunca derruba o app */ }
    }
}
