using System.Text.Json.Nodes;

namespace ClaudeWatch.Core;

/// <summary>
/// Lê a resposta de api.anthropic.com/api/oauth/usage (a mesma do <c>/usage</c> do CLI).
///
/// Formato atual: um array <c>limits</c> com um item por limite ativo do plano —
/// <c>{ kind, group, percent, resets_at, scope: { model: { display_name } } }</c>.
/// É a fonte preferida: traz só os limites que existem no plano (no Ultra verificado
/// não há sessão de 5h) e já nomeia o modelo dos limites por escopo ("Fable").
///
/// Formato antigo, usado quando <c>limits</c> não vem: buckets fixos
/// (<c>five_hour</c>, <c>seven_day</c>, <c>seven_day_sonnet</c>, <c>seven_day_opus</c>),
/// cada um <c>{ utilization, resets_at }</c> ou <c>null</c> quando a cota não existe no plano.
/// Bucket nulo agora é omitido, em vez de virar um medidor parado em 0%.
/// </summary>
public static class UsageResponseParser
{
    private static readonly TimeSpan Week = TimeSpan.FromDays(7);
    private static readonly TimeSpan Session = TimeSpan.FromHours(5);

    public static UsageSnapshot Parse(string json, DateTimeOffset now, TimeZoneInfo? tz = null)
    {
        var n = JsonNode.Parse(json)!;
        var weekStart = Date(n["seven_day_breakdown"]?["window_started_at"]);

        var meters = (FromLimits(n, weekStart) ?? FromBuckets(n))
            .Select(m => MeterHints.Annotate(m, now, tz ?? TimeZoneInfo.Local))
            .ToList();

        return new UsageSnapshot(meters, now, SnapshotState.Ok) { Breakdown = Breakdown(n) };
    }

    private static List<Meter>? FromLimits(JsonNode n, DateTimeOffset? weekStart)
    {
        if (n["limits"] is not JsonArray { Count: > 0 } limits) return null;

        // Índice original preservado: weekly_scoped_shares aponta para ele (limit_index).
        var list = new List<(int Order, int Index, Meter Meter)>();
        for (var i = 0; i < limits.Count; i++)
        {
            if (limits[i] is not JsonObject l) continue;
            var kind = Str(l["kind"]) ?? "";
            var group = Str(l["group"]) ?? "";
            var scopeName = Str(l["scope"]?["model"]?["display_name"])
                         ?? Str(l["scope"]?["surface"]?["display_name"]);
            var pct = l["percent"] is JsonValue p ? ZoneRules.Clamp((int)Math.Round(p.GetValue<double>())) : 0;
            var reset = Date(l["resets_at"]);

            var isSession = IsSession(kind) || IsSession(group);
            var isWeekly = group == "weekly" || kind.StartsWith("weekly", StringComparison.Ordinal);

            var (label, shortLabel, order) =
                scopeName is not null ? (scopeName, scopeName, 2)
                : isSession ? ("Sessão 5h", "5h", 0)
                : kind == "weekly_all" ? ("Semana", "Sem", 1)
                : (Pretty(kind), Pretty(kind), 3);

            DateTimeOffset? start = reset is null ? null
                : isSession ? reset - Session
                : isWeekly ? weekStart ?? reset - Week
                : null;

            var key = scopeName is null ? kind : $"{kind}:{scopeName.ToLowerInvariant()}";
            list.Add((order, i, new Meter(label, pct, reset) { Key = key, Short = shortLabel, WindowStart = start }));
        }

        ApplyShares(n, list);
        return list.OrderBy(x => x.Order).Select(x => x.Meter).ToList();
    }

    /// <summary>
    /// weekly_scoped_shares: quanto um limite por modelo (o Fable) já comeu da Semana
    /// e o teto dele — <c>{ limit_index, used_percent_of_weekly, allowance_percent_of_weekly }</c>.
    /// Vira a fatia desenhada dentro do medidor da Semana.
    /// </summary>
    private static void ApplyShares(JsonNode n, List<(int Order, int Index, Meter Meter)> list)
    {
        if (n["weekly_scoped_shares"] is not JsonArray shares) return;
        var week = list.FindIndex(x => x.Meter.Key == "weekly_all");
        if (week < 0) return;

        foreach (var sh in shares.OfType<JsonObject>())
        {
            if (sh["limit_index"] is not JsonValue li || sh["used_percent_of_weekly"] is not JsonValue used) continue;
            var scoped = list.FindIndex(x => x.Index == li.GetValue<int>());
            if (scoped < 0) continue;
            int? cap = sh["allowance_percent_of_weekly"] is JsonValue a ? (int)Math.Round(a.GetValue<double>()) : null;

            var w = list[week];
            list[week] = w with
            {
                Meter = w.Meter with
                {
                    Part = ZoneRules.Clamp((int)Math.Round(used.GetValue<double>())),
                    PartCap = cap,
                    PartLabel = list[scoped].Meter.Label,
                },
            };
            return; // uma fatia só: com mais de um limite por modelo, o primeiro
        }
    }

    private static IReadOnlyList<BreakdownRow> Breakdown(JsonNode n) =>
        n["seven_day_breakdown"]?["rows"] is JsonArray rows
            ? rows.OfType<JsonObject>()
                .Select(r => new BreakdownRow(
                    Str(r["display_name"]) ?? Str(r["key"]) ?? "?",
                    r["percent"] is JsonValue p ? (int)Math.Round(p.GetValue<double>()) : 0))
                .Where(r => r.Pct > 0)
                .ToList()
            : [];

    private static List<Meter> FromBuckets(JsonNode n)
    {
        var list = new List<Meter>();
        void Add(string key, string label, string shortLabel, TimeSpan window)
        {
            if (n[key] is not JsonObject b) return; // cota inexistente no plano
            var pct = b["utilization"] is JsonValue u ? ZoneRules.Clamp((int)Math.Round(u.GetValue<double>())) : 0;
            var reset = Date(b["resets_at"]);
            list.Add(new Meter(label, pct, reset) { Key = key, Short = shortLabel, WindowStart = reset - window });
        }

        Add("five_hour", "Sessão 5h", "5h", Session);
        Add("seven_day", "Semana", "Sem", Week);
        Add("seven_day_sonnet", "Sonnet", "Sonnet", Week);
        Add("seven_day_opus", "Opus", "Opus", Week);
        return list;
    }

    private static bool IsSession(string s) =>
        s.Contains("five_hour", StringComparison.Ordinal) || s.Contains("session", StringComparison.Ordinal);

    private static string Pretty(string kind)
    {
        var s = kind.Replace('_', ' ').Trim();
        return s.Length == 0 ? "Limite" : char.ToUpperInvariant(s[0]) + s[1..];
    }

    private static string? Str(JsonNode? v) => v is JsonValue j && j.TryGetValue<string>(out var s) ? s : null;

    private static DateTimeOffset? Date(JsonNode? v) =>
        Str(v) is { } s && DateTimeOffset.TryParse(s, out var d) ? d : null;
}
