using System.Windows;
using System.Windows.Input;
using ClaudeWatch.Infrastructure;

namespace ClaudeWatch.Widget;

public partial class WidgetWindow : Window
{
    private readonly SettingsStore _store;
    private bool _locked;

    public WidgetWindow(SettingsStore store, Settings s)
    {
        InitializeComponent();
        _store = store;
        _locked = s.Locked;
        SourceInitialized += (_, _) =>
        {
            WindowInterop.ApplyWidgetStyles(this);
            WindowInterop.SetClickThrough(this, _locked);
        };
        Loaded += (_, _) => Restore(s);
    }

    public void SetLocked(bool locked)
    { _locked = locked; WindowInterop.SetClickThrough(this, locked); }

    public void ReassertTopmost() { Topmost = false; Topmost = true; }

    private void OnDragStart(object sender, MouseButtonEventArgs e)
    {
        if (_locked || e.LeftButton != MouseButtonState.Pressed) return;
        DragMove();
        var s = _store.Load();
        _store.Save(s with { PosX = Left, PosY = Top });
    }

    private void Restore(Settings s)
    {
        var x = s.PosX ?? SystemParameters.WorkArea.Right - ActualWidth - 24;
        var y = s.PosY ?? 24;
        Left = Math.Clamp(x, SystemParameters.VirtualScreenLeft,
            SystemParameters.VirtualScreenLeft + SystemParameters.VirtualScreenWidth - ActualWidth);
        Top = Math.Clamp(y, SystemParameters.VirtualScreenTop,
            SystemParameters.VirtualScreenTop + SystemParameters.VirtualScreenHeight - ActualHeight);
    }
}
