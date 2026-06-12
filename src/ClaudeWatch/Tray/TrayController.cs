using System.Windows.Forms;
using ClaudeWatch.Core;

namespace ClaudeWatch.Tray;

public sealed class TrayController : IDisposable
{
    private readonly NotifyIcon _icon;
    private System.Drawing.Icon? _current;
    private readonly ToolStripMenuItem _lock, _autostart, _skinAneis, _skinLed;

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

        var menu = new ContextMenuStrip();
        menu.Items.Add("Mostrar/ocultar widget", null, (_, _) => ToggleWidget?.Invoke());
        menu.Items.Add(_lock);
        menu.Items.Add(estilo);
        menu.Items.Add("Atualizar agora", null, (_, _) => RefreshNow?.Invoke());
        menu.Items.Add(_autostart);
        menu.Items.Add(new ToolStripSeparator());
        menu.Items.Add("Sair", null, (_, _) => ExitApp?.Invoke());

        _icon = new NotifyIcon { Visible = true, ContextMenuStrip = menu, Text = "ClaudeWatch" };
        _icon.DoubleClick += (_, _) => ToggleWidget?.Invoke();
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

    public void Dispose() { _icon.Visible = false; _icon.Dispose(); _current?.Dispose(); }
}
