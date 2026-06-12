namespace ClaudeWatch.Credentials;

public sealed class FileCredentialFile : ICredentialFile, IDisposable
{
    private readonly string _path;
    private readonly FileSystemWatcher? _watcher;
    public event Action? Changed;

    public FileCredentialFile(string path)
    {
        _path = path;
        var dir = Path.GetDirectoryName(path);
        if (dir is not null && Directory.Exists(dir))
        {
            _watcher = new FileSystemWatcher(dir, Path.GetFileName(path))
            { EnableRaisingEvents = true, NotifyFilter = NotifyFilters.LastWrite | NotifyFilters.FileName };
            _watcher.Changed += (_, _) => Changed?.Invoke();
            _watcher.Created += (_, _) => Changed?.Invoke();
            _watcher.Renamed += (_, _) => Changed?.Invoke();
        }
    }

    public string? ReadOrNull()
    {
        try { return File.Exists(_path) ? File.ReadAllText(_path) : null; }
        catch { return null; } // arquivo pode estar em escrita pelo CC; próxima leitura resolve
    }

    public void Dispose() => _watcher?.Dispose();
}
