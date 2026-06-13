using System.Windows.Forms;
using ClaudeWatch.Core;

namespace ClaudeWatch.Tray;

public sealed class TrayController : IDisposable
{
    private readonly NotifyIcon _icon;
    private System.Drawing.Icon? _current;
    private readonly ToolStripMenuItem _lock, _autostart, _skinAneis, _skinLed;
    private readonly ContextMenuStrip _menu;
    private ToolStripMenuItem? _updateItem;
    private string? _updateUrl;
    private string? _lastNotifiedVersion;

    public event Action? ToggleWidget, RefreshNow, ExitApp;
    public event Action<bool>? LockChanged, AutostartChanged;
    public event Action<string>? SkinChanged;

    public TrayController(bool locked, bool autostart, string skin)
    {
        _lock = new ToolStripMenuItem("Travar widget") { CheckOnClick = true, Checked = locked };
        _lock.CheckedChanged += (_, _) => LockChanged?.Invoke(_lock.Checked);

        _autostart = new ToolStripMenuItem("Iniciar com o Windows") { CheckOnClick = true, Checked = autostart };
        _autostart.CheckedChanged += (_, _) => AutostartChanged?.Invoke(_autostart.Checked);

        _skinAneis = new ToolStripMenuItem("Anéis") { Checked = skin == "Aneis" };
        _skinLed = new ToolStripMenuItem("LED") { Checked = skin == "Led" };
        _skinAneis.Click += (_, _) => SelectSkin("Aneis");
        _skinLed.Click += (_, _) => SelectSkin("Led");
        var estilo = new ToolStripMenuItem("Estilo");
        estilo.DropDownItems.AddRange([_skinAneis, _skinLed]);

        _menu = new ContextMenuStrip();
        _menu.Items.Add("Mostrar/ocultar widget", null, (_, _) => ToggleWidget?.Invoke());
        _menu.Items.Add(_lock);
        _menu.Items.Add(estilo);
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
        _skinAneis.Checked = skin == "Aneis";
        _skinLed.Checked = skin == "Led";
        SkinChanged?.Invoke(skin);
    }

    public void Update(UsageSnapshot s)
    {
        var next = IconRenderer.Render(s.Worst);
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
