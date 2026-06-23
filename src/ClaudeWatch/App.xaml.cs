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
    private Mutex? _mutex;
    private EventWaitHandle? _showSignal;

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        _mutex = new Mutex(true, @"Global\ClaudeWatch.Widget", out var first);
        _showSignal = new EventWaitHandle(false, EventResetMode.AutoReset, @"Global\ClaudeWatch.Show");
        if (!first) { _showSignal.Set(); Shutdown(); return; }
        var showThread = new Thread(() =>
        {
            while (_showSignal.WaitOne())
                Dispatcher.Invoke(() => { _window?.Show(); _window?.Activate(); });
        }) { IsBackground = true };
        showThread.Start();

        var log = new FileLogger(AppPaths.LogsDir);
        var store = new SettingsStore(AppPaths.BaseDir);
        var settings = store.Load();

        var home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        _credFile = new FileCredentialFile(CredentialPaths.Resolve(home, settings));
        var http = new HttpClient();
        var pipeline = new CredentialPipeline(_credFile);
        var usage = new UsageClient(http);

        var vm = new WidgetViewModel { Skin = settings.Skin };
        _window = new WidgetWindow(store, settings) { DataContext = vm };
        _tray = new TrayController(settings.Locked, Autostart.IsEnabled(), settings.Skin);

        var demo = Showroom.Mode is not null;
        var poller = new UsagePoller(
            getToken: demo ? _ => Task.FromResult(new TokenResult("demo", SnapshotState.Ok))
                           : ct => pipeline.GetAccessTokenAsync(DateTimeOffset.UtcNow, ct),
            fetch: demo ? (_, _) => Task.FromResult(Showroom.Snapshot())
                        : usage.FetchAsync,
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

        var updateService = new UpdateService(
            AppVersion.Current,
            new GitHubReleaseClient(http),
            publish: s => Dispatcher.Invoke(() =>
            {
                if (!s.Available) return;
                _tray!.ShowUpdate(s.LatestVersion!, s.Url!);
                vm.UpdateAvailable = true;
                vm.UpdateLabel = $"⬆ v{s.LatestVersion} disponível";
                vm.UpdateUrl = s.Url;
            }),
            log: log.Log);
        _ = updateService.RunAsync(TimeSpan.FromSeconds(10), TimeSpan.FromHours(6), _cts.Token);
    }

    protected override void OnExit(ExitEventArgs e)
    {
        _cts?.Cancel(); _tray?.Dispose(); _credFile?.Dispose();
        _showSignal?.Dispose(); _mutex?.Dispose();
        base.OnExit(e);
    }
}
