using System.ComponentModel;
using ClaudeWatch.Core;

namespace ClaudeWatch.Widget;

public sealed class WidgetViewModel : INotifyPropertyChanged
{
    private UsageSnapshot? _snapshot;
    private string _skin = "Aneis";
    public event PropertyChangedEventHandler? PropertyChanged;

    public UsageSnapshot? Snapshot
    { get => _snapshot; set { _snapshot = value; Raise(nameof(Snapshot)); } }

    public string Skin
    { get => _skin; set { _skin = value; Raise(nameof(Skin)); } }

    private bool _updateAvailable;
    private string? _updateLabel;
    private string? _updateUrl;

    public bool UpdateAvailable { get => _updateAvailable; set { _updateAvailable = value; Raise(nameof(UpdateAvailable)); } }
    public string? UpdateLabel { get => _updateLabel; set { _updateLabel = value; Raise(nameof(UpdateLabel)); } }
    public string? UpdateUrl { get => _updateUrl; set { _updateUrl = value; Raise(nameof(UpdateUrl)); } }

    private void Raise(string n) => PropertyChanged?.Invoke(this, new(n));
}
