using System.Text.Json.Nodes;

namespace ClaudeWatch.Core;

/// <summary>
/// Lê o arquivo que a ponte de statusline grava (<see cref="Infrastructure.StatuslineBridge"/>):
/// <c>{ "captured_at": ISO, "rate_limits": { ... } }</c>, com <c>rate_limits</c> exatamente como o
/// Claude Code entrega aos scripts de statusline — fonte oficial e documentada:
/// https://code.claude.com/docs/en/statusline
///
/// Cada janela é <c>{ used_percentage: 0–100, resets_at: epoch em segundos }</c> e pode faltar.
/// Só existem <c>five_hour</c> e <c>seven_day</c> (e <c>spend_limit</c> atrás de um gateway,
/// ignorado aqui): limites por modelo, como o do Fable, não vêm por esta fonte.
/// </summary>
public static class StatuslineParser
{
    private static readonly TimeSpan Week = TimeSpan.FromDays(7);
    private static readonly TimeSpan Session = TimeSpan.FromHours(5);

    public static UsageSnapshot Parse(string json, DateTimeOffset now, TimeZoneInfo? tz = null)
    {
        var n = JsonNode.Parse(json)!;
        var captured = n["captured_at"] is JsonValue c && c.TryGetValue<string>(out var s)
                       && DateTimeOffset.TryParse(s, out var d) ? d : now;
        var rl = n["rate_limits"] as JsonObject;

        var meters = new List<Meter>();
        void Add(string key, string label, string shortLabel, TimeSpan window)
        {
            if (rl?[key] is not JsonObject w) return;
            var pct = w["used_percentage"] is JsonValue p ? ZoneRules.Clamp((int)Math.Round(p.GetValue<double>())) : 0;
            DateTimeOffset? reset = w["resets_at"] is JsonValue r && r.TryGetValue<double>(out var secs)
                ? DateTimeOffset.FromUnixTimeSeconds((long)secs) : null;

            // A janela virou desde a captura: o uso recomeçou do zero e o próximo reset
            // só chega com a próxima resposta do Claude Code. (O Claude Code faz o mesmo:
            // descarta a janela quando o resets_at passa.)
            if (reset is { } x && x <= now) { pct = 0; reset = null; }

            meters.Add(new Meter(label, pct, reset) { Key = key, Short = shortLabel, WindowStart = reset - window });
        }

        Add("five_hour", "Sessão 5h", "5h", Session);
        Add("seven_day", "Semana", "Sem", Week);

        var zone = tz ?? TimeZoneInfo.Local;
        return new UsageSnapshot(meters.Select(m => MeterHints.Annotate(m, now, zone)).ToList(), captured, SnapshotState.Ok);
    }
}
