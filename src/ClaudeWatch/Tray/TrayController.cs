using System.Windows.Forms;
using ClaudeWatch.Core;

namespace ClaudeWatch.Tray;

public sealed class TrayController : IDisposable
{
    private readonly NotifyIcon _icon;
    private System.Drawing.Icon? _current;
    private readonly ToolStripMenuItem _lock, _autostart;
    private readonly (string Key, ToolStripMenuItem Item)[] _skins;
    private readonly ToolStripMenuItem _srcStatusline, _srcApi;
    private readonly ContextMenuStrip _menu;
    private ToolStripMenuItem? _updateItem;
    private string? _updateUrl;
    private string? _lastNotifiedVersion;

    public event Action? ToggleWidget, RefreshNow, ExitApp, SetupStatusline, OpenDocs;
    /// <summary>A pessoa escolheu outra fonte no menu; o app confirma e chama <see cref="SetSource"/>.</summary>
    public event Action<string>? SourceRequested;
    public event Action<bool>? LockChanged, AutostartChanged;
    public event Action<string>? SkinChanged;

    public TrayController(bool locked, bool autostart, string skin, string source)
    {
        _srcStatusline = new ToolStripMenuItem("Statusline do Claude Code (oficial)");
        _srcApi = new ToolStripMenuItem("API de uso (não oficial, com Fable)");
        _srcStatusline.Click += (_, _) => SourceRequested?.Invoke(Infrastructure.Sources.Statusline);
        _srcApi.Click += (_, _) => SourceRequested?.Invoke(Infrastructure.Sources.Api);
        var fonte = new ToolStripMenuItem("Fonte dos dados");
        fonte.DropDownItems.AddRange([_srcStatusline, _srcApi, new ToolStripSeparator()]);
        fonte.DropDownItems.Add("Configurar a statusline do Claude Code…", null, (_, _) => SetupStatusline?.Invoke());
        fonte.DropDownItems.Add("Como funciona…", null, (_, _) => OpenDocs?.Invoke());
        SetSource(source);

        _lock = new ToolStripMenuItem("Travar widget") { CheckOnClick = true, Checked = locked };
        _lock.CheckedChanged += (_, _) => LockChanged?.Invoke(_lock.Checked);

        _autostart = new ToolStripMenuItem("Iniciar com o Windows") { CheckOnClick = true, Checked = autostart };
        _autostart.CheckedChanged += (_, _) => AutostartChanged?.Invoke(_autostart.Checked);

        _skins =
        [
            ("Aneis", new ToolStripMenuItem("Anéis")),
            ("Barras", new ToolStripMenuItem("Barras")),
            ("Compacto", new ToolStripMenuItem("Compacto")),
            ("Led", new ToolStripMenuItem("LED")),
        ];
        var estilo = new ToolStripMenuItem("Estilo");
        foreach (var (key, item) in _skins)
        {
            item.Checked = key == skin;
            item.Click += (_, _) => SelectSkin(key);
            estilo.DropDownItems.Add(item);
        }

        _menu = new ContextMenuStrip();
        _menu.Items.Add("Mostrar/ocultar widget", null, (_, _) => ToggleWidget?.Invoke());
        _menu.Items.Add(_lock);
        _menu.Items.Add(estilo);
        _menu.Items.Add(fonte);
        _menu.Items.Add("Atualizar agora", null, (_, _) => RefreshNow?.Invoke());
        _menu.Items.Add(_autostart);
        _menu.Items.Add(new ToolStripSeparator());
        _menu.Items.Add("Sair", null, (_, _) => ExitApp?.Invoke());

        _icon = new NotifyIcon { Visible = true, ContextMenuStrip = _menu, Text = "ClaudeWatch" };
        _icon.DoubleClick += (_, _) => ToggleWidget?.Invoke();
        _icon.BalloonTipClicked += (_, _) => OpenUpdateUrl();
    }

    private void SelectSkin(string skin)
    {
        foreach (var (key, item) in _skins) item.Checked = key == skin;
        SkinChanged?.Invoke(skin);
    }

    public void SetSource(string source)
    {
        _srcStatusline.Checked = source == Infrastructure.Sources.Statusline;
        _srcApi.Checked = source == Infrastructure.Sources.Api;
    }

    public void Notify(string text)
    {
        _icon.BalloonTipTitle = "ClaudeWatch";
        _icon.BalloonTipText = text;
        _icon.ShowBalloonTip(6000);
    }

    public void Update(UsageSnapshot s)
    {
        var next = IconRenderer.Render(s.Critical);
        _icon.Icon = next;
        _icon.Text = TooltipFormatter.Format(s);
        _current?.Dispose();
        _current = next;
    }

    public void ShowUpdate(string version, string url)
    {
        _updateUrl = url;

        if (_updateItem is null)
        {
            _updateItem = new ToolStripMenuItem { ForeColor = System.Drawing.Color.FromArgb(0x4C, 0x9A, 0xFF) };
            _updateItem.Click += (_, _) => OpenUpdateUrl();
            _menu.Items.Insert(0, _updateItem);
            _menu.Items.Insert(1, new ToolStripSeparator());
        }
        _updateItem.Text = $"⬆ Baixar atualização ({version})";

        if (_lastNotifiedVersion != version) // balão só uma vez por versão
        {
            _lastNotifiedVersion = version;
            _icon.BalloonTipTitle = "ClaudeWatch";
            _icon.BalloonTipText = $"Versão {version} disponível — clique para baixar";
            _icon.ShowBalloonTip(5000);
        }
    }

    private void OpenUpdateUrl()
    {
        if (_updateUrl is null) return;
        try { System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(_updateUrl) { UseShellExecute = true }); }
        catch { /* sem navegador: ignora */ }
    }

    public void Dispose() { _icon.Visible = false; _icon.Dispose(); _current?.Dispose(); }
}
