namespace ClaudeWatch.Core;

public sealed class BackoffPolicy
{
    private static readonly TimeSpan[] Steps =
        [TimeSpan.FromMinutes(1), TimeSpan.FromMinutes(2), TimeSpan.FromMinutes(5)];
    private int _failures;

    public void Failure() => _failures++;
    public void Success() => _failures = 0;
    public TimeSpan NextDelay(TimeSpan normal) =>
        _failures == 0 ? normal : Steps[Math.Min(_failures - 1, Steps.Length - 1)];
}
