using System.Diagnostics;
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
    private FileCredentialFile? _credFile, _statuslineFile;
    private CancellationTokenSource? _cts;
    private Mutex? _mutex;
    private EventWaitHandle? _showSignal;
    private string _source = Sources.Statusline;

    private static readonly string Home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
    private static string ClaudeSettings => ClaudeStatuslineSetup.SettingsPath(Home);

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        // Chamado pelo Claude Code como statusline: grava os limites e sai, sem abrir nada.
        if (e.Args.Contains(StatuslineBridge.Arg) || e.Args.Contains(StatuslineBridge.CaptureArg))
        {
            Shutdown(StatuslineBridge.Run(print: !e.Args.Contains(StatuslineBridge.CaptureArg)));
            return;
        }

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
        Directory.CreateDirectory(AppPaths.BaseDir);

        // Primeira execução (ou atualização vinda da v0.3): perguntar a fonte uma vez.
        // Fechar a janela sem escolher não é consentimento para mexer no settings.json do
        // Claude Code: segue com a fonte oficial sem configurar nada e pergunta de novo depois.
        var askedNow = false;
        if (settings.Source is null)
        {
            var dialog = new SourceDialog();
            if (dialog.ShowDialog() == true && dialog.Choice is { } chosen)
            {
                settings = settings with { Source = chosen };
                store.Save(settings);
                askedNow = true;
            }
        }
        _source = settings.Source ?? Sources.Statusline;

        _credFile = new FileCredentialFile(CredentialPaths.Resolve(Home, settings));
        _statuslineFile = new FileCredentialFile(StatuslineBridge.DataPath);
        var http = new HttpClient();
        var pipeline = new CredentialPipeline(_credFile);
        var usage = new UsageClient(http);

        var vm = new WidgetViewModel { Skin = settings.Skin };
        _window = new WidgetWindow(store, settings) { DataContext = vm };
        _tray = new TrayController(settings.Locked, Autostart.IsEnabled(), settings.Skin, _source);

        if (settings.Source == Sources.Statusline) EnsureStatusline(routine: !askedNow);

        var demo = Showroom.Mode is not null;
        var poller = new UsagePoller(
            getToken: demo ? _ => Task.FromResult(new TokenResult("demo", SnapshotState.Ok))
                     : ct => _source == Sources.Api
                         ? pipeline.GetAccessTokenAsync(DateTimeOffset.UtcNow, ct)
                         : Task.FromResult(_statuslineFile.ReadOrNull() is null
                             ? new TokenResult(null, SnapshotState.WaitingStatusline)
                             : new TokenResult("statusline", SnapshotState.Ok)),
            fetch: demo ? (_, _) => Task.FromResult(Showroom.Snapshot())
                  : (token, ct) => _source == Sources.Api
                      ? usage.FetchAsync(token, ct)
                      : Task.FromResult(StatuslineParser.Parse(
                          _statuslineFile.ReadOrNull() ?? throw new FileNotFoundException(StatuslineBridge.DataPath),
                          DateTimeOffset.Now)),
            publish: s => Dispatcher.Invoke(() =>
            { vm.Snapshot = s; _tray.Update(s); _window.ReassertTopmost(); }),
            log: log.Log);

        _cts = new CancellationTokenSource();
        _credFile.Changed += () => { if (_source == Sources.Api) _ = poller.TickAsync(_cts.Token); };
        _statuslineFile.Changed += () => { if (_source == Sources.Statusline) _ = poller.TickAsync(_cts.Token); };
        _tray.RefreshNow += () => _ = poller.TickAsync(_cts.Token);
        _tray.ToggleWidget += () =>
        { if (_window.IsVisible) _window.Hide(); else _window.Show(); };
        _tray.LockChanged += locked =>
        { _window.SetLocked(locked); store.Save(store.Load() with { Locked = locked }); };
        _tray.SkinChanged += skin =>
        { vm.Skin = skin; store.Save(store.Load() with { Skin = skin }); };
        _tray.SourceRequested += source =>
        {
            if (source == _source) return;
            if (source == Sources.Api && !ConfirmApi()) { _tray.SetSource(_source); return; }
            _source = source;
            store.Save(store.Load() with { Source = source });
            _tray.SetSource(source);
            if (source == Sources.Statusline) EnsureStatusline(routine: false);
            _ = poller.TickAsync(_cts.Token);
        };
        _tray.SetupStatusline += () => EnsureStatusline(routine: false);
        _tray.OpenDocs += () => Open(AppUrls.Docs);
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

    /// <summary>
    /// Configura o Claude Code para chamar o ClaudeWatch como statusline (decisão em
    /// <see cref="ClaudeStatuslineSetup.Plan"/>). Na <paramref name="routine"/> (o app abrindo) só
    /// acompanha o .exe que mudou de pasta; instalar ou explicar, só quando a pessoa pede.
    /// Há outra statusline: não mexe, explica como encadear e copia a linha para a área de transferência.
    /// </summary>
    private void EnsureStatusline(bool routine)
    {
        var exe = Environment.ProcessPath;
        if (exe is null) return;
        var (state, command) = ClaudeStatuslineSetup.Inspect(ClaudeSettings);
        try
        {
            switch (ClaudeStatuslineSetup.Plan(state, command, exe, routine))
            {
                case StatuslineAction.Install:
                    ClaudeStatuslineSetup.Install(ClaudeSettings, exe);
                    _tray?.Notify("Statusline do Claude Code configurada. Os limites aparecem na próxima resposta do Claude Code.");
                    break;
                case StatuslineAction.UpdatePath:
                    ClaudeStatuslineSetup.Install(ClaudeSettings, exe); // o .exe mudou de pasta
                    break;
                case StatuslineAction.AlreadyOurs:
                    _tray?.Notify("A statusline do Claude Code já é a do ClaudeWatch.");
                    break;
                case StatuslineAction.ExplainOther:
                    var snippet = ClaudeStatuslineSetup.BashSnippet(exe);
                    try { Clipboard.SetText(snippet); } catch { /* área de transferência ocupada */ }
                    MessageBox.Show(
                        "Você já tem uma statusline no Claude Code:\n\n" + command + "\n\n" +
                        "O ClaudeWatch não vai substituí-la. Para ele receber os limites também, coloque esta linha " +
                        "no começo do seu script (já está na área de transferência) e use $input no lugar do stdin:\n\n" +
                        snippet + "\n\nDetalhes em \"Como funciona…\", no menu Fonte dos dados.",
                        "ClaudeWatch — statusline", MessageBoxButton.OK, MessageBoxImage.Information);
                    break;
                case StatuslineAction.ExplainUnreadable:
                    MessageBox.Show(
                        "Não consegui ler ~/.claude/settings.json como JSON, então não mexi nele.\n\n" +
                        "Para configurar à mão, adicione:\n\n\"statusLine\": { \"type\": \"command\", \"command\": "
                        + System.Text.Json.JsonSerializer.Serialize(ClaudeStatuslineSetup.Command(exe)) + " }",
                        "ClaudeWatch — statusline", MessageBoxButton.OK, MessageBoxImage.Warning);
                    break;
            }
        }
        catch (Exception ex)
        {
            MessageBox.Show("Não consegui configurar a statusline do Claude Code: " + ex.Message,
                "ClaudeWatch — statusline", MessageBoxButton.OK, MessageBoxImage.Warning);
        }
    }

    private static bool ConfirmApi() => MessageBox.Show(
        "A API de uso traz o limite do Fable e de onde veio o consumo, mas:\n\n" +
        "• é um endpoint sem documentação, que pode mudar ou parar sem aviso;\n" +
        "• o ClaudeWatch lê o token do Claude Code para chamá-lo, e os termos da Anthropic restringem " +
        "o uso desse token fora do Claude Code.\n\nUsar mesmo assim?",
        "ClaudeWatch — API de uso (não oficial)", MessageBoxButton.YesNo, MessageBoxImage.Warning) == MessageBoxResult.Yes;

    private static void Open(string url)
    {
        try { Process.Start(new ProcessStartInfo(url) { UseShellExecute = true }); }
        catch { /* sem navegador */ }
    }

    protected override void OnExit(ExitEventArgs e)
    {
        _cts?.Cancel(); _tray?.Dispose(); _credFile?.Dispose(); _statuslineFile?.Dispose();
        _showSignal?.Dispose(); _mutex?.Dispose();
        base.OnExit(e);
    }
}
