using System.ComponentModel;
using ClaudeWatch.Core;

namespace ClaudeWatch.Widget;

public sealed class WidgetViewModel : INotifyPropertyChanged
{
    private UsageSnapshot? _snapshot;
    private string _skin = "Aneis";
    public event PropertyChangedEventHandler? PropertyChanged;

    public UsageSnapshot? Snapshot
    {
        get => _snapshot;
        set
        {
            _snapshot = value;
            Raise(nameof(Snapshot)); Raise(nameof(Critical)); Raise(nameof(Others));
            Raise(nameof(HasOthers)); Raise(nameof(Rings));
        }
    }

    /// <summary>O medidor em destaque: centro do anel e começo da pílula compacta.</summary>
    public Meter? Critical => _snapshot?.Critical;

    /// <summary>Os demais medidores, na ordem, para o resto da pílula compacta.</summary>
    public IReadOnlyList<Meter> Others =>
        _snapshot is null ? [] : _snapshot.Meters.Where(m => !ReferenceEquals(m, _snapshot.Critical)).ToList();

    public bool HasOthers => _snapshot is { Meters.Count: > 1 };

    public IReadOnlyList<RingVisual> Rings => RingVisual.Build(_snapshot?.Meters ?? []);

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
