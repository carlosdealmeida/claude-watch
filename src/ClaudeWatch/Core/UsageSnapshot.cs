namespace ClaudeWatch.Core;

public enum SnapshotState { Ok, Stale, NoCredential }

public sealed record Meter(string Label, int Pct, DateTimeOffset? ResetAt)
{
    public Zone Zone => ZoneRules.From(Pct);
}

public sealed record UsageSnapshot(
    Meter FiveHour, Meter Week, Meter Opus,
    DateTimeOffset CollectedAt, SnapshotState State)
{
    public Meter Worst => new[] { FiveHour, Week, Opus }.MaxBy(m => m.Pct)!;
}
