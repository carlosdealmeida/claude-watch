namespace ClaudeWatch.Core;

public sealed class UsagePoller(
    Func<CancellationToken, Task<string?>> getToken,
    Func<string, CancellationToken, Task<UsageSnapshot>> fetch,
    Action<UsageSnapshot> publish,
    Action<string> log)
{
    public BackoffPolicy Backoff { get; } = new();
    private UsageSnapshot? _last;

    public async Task RunAsync(TimeSpan interval, CancellationToken ct)
    {
        while (!ct.IsCancellationRequested)
        {
            await TickAsync(ct);
            try { await Task.Delay(Backoff.NextDelay(interval), ct); }
            catch (OperationCanceledException) { break; }
        }
    }

    public async Task TickAsync(CancellationToken ct)
    {
        try
        {
            var token = await getToken(ct);
            if (token is null) { publish(WithState(SnapshotState.NoCredential)); return; }
            _last = await fetch(token, ct);
            Backoff.Success();
            publish(_last);
        }
        catch (Exception ex)
        {
            log($"tick: {ex.GetType().Name}: {ex.Message}");
            Backoff.Failure();
            publish(WithState(SnapshotState.Stale));
        }
    }

    private UsageSnapshot WithState(SnapshotState st) =>
        (_last ?? Empty()) with { State = st };

    private static UsageSnapshot Empty() => new(
        new Meter("Sessão 5h", 0, null), new Meter("Semana", 0, null),
        new Meter("Sonnet", 0, null), DateTimeOffset.UtcNow, SnapshotState.Stale);
}
