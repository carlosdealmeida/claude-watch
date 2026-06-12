using System.Text.Json.Nodes;

namespace ClaudeWatch.Core;

public static class UsageResponseParser
{
    // TODO(carlos): ajustar chaves ao shape real (parity do CORTEX).
    public static UsageSnapshot Parse(string json, DateTimeOffset now)
    {
        var n = JsonNode.Parse(json)!;
        Meter M(string key, string label) => new(
            label,
            ZoneRules.Clamp((int)Math.Round(n[key]!["utilization"]!.GetValue<double>())),
            n[key]!["resets_at"] is { } r ? DateTimeOffset.Parse(r.GetValue<string>()) : null);
        return new UsageSnapshot(M("five_hour", "Sessão 5h"), M("seven_day", "Semana"),
            M("seven_day_opus", "Opus"), now, SnapshotState.Ok);
    }
}
