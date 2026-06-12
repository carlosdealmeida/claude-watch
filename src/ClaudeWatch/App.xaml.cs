using System.Net.Http;
using System.Windows;
using ClaudeWatch.Core;
using ClaudeWatch.Credentials;
using ClaudeWatch.Infrastructure;
using ClaudeWatch.Tray;
using ClaudeWatch.Widget;

namespace ClaudeWatch;

public partial class App : Application
{
    private TrayController? _tray;
    private WidgetWindow? _window;
    private FileCredentialFile? _credFile;
    private CancellationTokenSource? _cts;

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);
        var log = new FileLogger(AppPaths.LogsDir);
        var store = new SettingsStore(AppPaths.BaseDir);
        var settings = store.Load();

        var home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        _credFile = new FileCredentialFile(CredentialPaths.Resolve(home, settings));
        var http = new HttpClient();
        var pipeline = new CredentialPipeline(_credFile, new TokenCache(AppPaths.BaseDir),
            new OAuthRefreshClient(http), log.Log);
        var usage = new UsageClient(http);

        var vm = new WidgetViewModel { Skin = settings.Skin };
        _window = new WidgetWindow(store, settings) { DataContext = vm };
        _tray = new TrayController(settings.Locked, Autostart.IsEnabled(), settings.Skin);

        var poller = new UsagePoller(
            getToken: ct => pipeline.GetAccessTokenAsync(DateTimeOffset.UtcNow, ct),
            fetch: usage.FetchAsync,
            publish: s => Dispatcher.Invoke(() =>
            { vm.Snapshot = s; _tray.Update(s); _window.ReassertTopmost(); }),
            log: log.Log);

        _cts = new CancellationTokenSource();
        _credFile.Changed += () => _ = poller.TickAsync(_cts.Token);
        _tray.RefreshNow += () => _ = poller.TickAsync(_cts.Token);
        _tray.ToggleWidget += () =>
        { if (_window.IsVisible) _window.Hide(); else _window.Show(); };
        _tray.LockChanged += locked =>
        { _window.SetLocked(locked); store.Save(store.Load() with { Locked = locked }); };
        _tray.SkinChanged += skin =>
        { vm.Skin = skin; store.Save(store.Load() with { Skin = skin }); };
        _tray.AutostartChanged += Autostart.Set;
        _tray.ExitApp += () => { _cts.Cancel(); Shutdown(); };

        _window.Show();
        _ = poller.RunAsync(TimeSpan.FromSeconds(Math.Max(15, settings.IntervalSeconds)), _cts.Token);
    }

    protected override void OnExit(ExitEventArgs e)
    {
        _cts?.Cancel(); _tray?.Dispose(); _credFile?.Dispose();
        base.OnExit(e);
    }
}
