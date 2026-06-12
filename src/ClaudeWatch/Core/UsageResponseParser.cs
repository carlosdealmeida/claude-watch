using System.Text.Json.Nodes;

namespace ClaudeWatch.Core;

public static class UsageResponseParser
{
    // Shape do endpoint api.anthropic.com/api/oauth/usage (Claude Code CLI):
    // cada bucket é { utilization: num|null, resets_at: ISO|null } ou o bucket inteiro é null
    // (cota inativa no plano — ex.: seven_day_opus em planos sem Opus).
    public static UsageSnapshot Parse(string json, DateTimeOffset now)
    {
        var n = JsonNode.Parse(json)!;

        Meter M(string key, string label)
        {
            var bucket = n[key];
            if (bucket is null) return new Meter(label, 0, null);
            var pct = bucket["utilization"] is { } u
                ? ZoneRules.Clamp((int)Math.Round(u.GetValue<double>())) : 0;
            var reset = bucket["resets_at"] is { } r
                ? DateTimeOffset.Parse(r.GetValue<string>()) : (DateTimeOffset?)null;
            return new Meter(label, pct, reset);
        }

        return new UsageSnapshot(M("five_hour", "Sessão 5h"), M("seven_day", "Semana"),
            M("seven_day_opus", "Opus"), now, SnapshotState.Ok);
    }
}
