# How ClaudeWatch reads your limits

<p><a href="LIMITES.md">Português</a> · <b>English</b> · <a href="LIMITES.es.md">Español</a></p>

Anthropic doesn't document a public API for "how much of my subscription have I used". This page collects what ClaudeWatch relies on to show your limits: **where the data comes from, what each field means, and how to work out your pace**. You get the widget (the fish) and enough to build your own tool (the rod).

> The formats here were **observed on 2026-11-24**. The official source is documented; the other one isn't and may change without notice. Each section says which is which.

## The two sources

| | **Claude Code status line** (default) | **Usage API** (opt-in) |
|---|---|---|
| Status | Official and documented | Unofficial, undocumented |
| Provides | 5-hour session and week | Everything: session, week, **Fable limit**, Fable's share of the week, where usage came from |
| Updates | On every Claude Code response | When ClaudeWatch polls (every minute) |
| Needs | A Claude Code session in use | The Claude Code token |
| Risk | None | The endpoint may change or stop; the [terms](https://code.claude.com/docs/en/legal-and-compliance) restrict using the token outside Claude Code |

You pick the source on first run, and later from the tray icon menu under **Fonte dos dados** (Data source).

## Source 1 — the Claude Code status line (official)

Claude Code runs a command of yours as its [status line](https://code.claude.com/docs/en/statusline) and pipes a JSON with the session state to its stdin. For subscribers, that JSON includes `rate_limits`:

```json
"rate_limits": {
  "five_hour": { "used_percentage": 23.5, "resets_at": 1795528800 },
  "seven_day": { "used_percentage": 12,   "resets_at": 1796040000 }
}
```

| Field | Meaning |
|---|---|
| `used_percentage` | How much of the window is used, 0 to 100 |
| `resets_at` | When the window resets, in **Unix seconds** (epoch) |

Rules from the official docs that matter when reading this data:

- `rate_limits` only appears **after the session's first response**.
- Each window may be missing on its own. Verified Ultra accounts have no `five_hour`.
- Once a window's `resets_at` passes, Claude Code drops that window.
- Behind a gateway with a spend limit there is also `spend_limit`. ClaudeWatch ignores it.

### What ClaudeWatch does with it

With the official source, ClaudeWatch sets itself as the status line in `~/.claude/settings.json`:

```json
"statusLine": { "type": "command", "command": "\"C:/Users/you/Apps/ClaudeWatch.exe\" --statusline" }
```

Before touching the file it saves a backup to `settings.json.claudewatch.bak`, keeps everything else as is, and **never replaces an existing status line**.

When Claude Code calls it, `ClaudeWatch.exe --statusline` does two things and exits:

1. It writes **only the limits** to `%APPDATA%\ClaudeWatch\statusline.json`. Directory, session and model are left out:
   ```json
   { "captured_at": "2026-11-24T09:31:43-03:00", "rate_limits": { "seven_day": { "used_percentage": 12, "resets_at": 1796040000 } } }
   ```
2. It prints a line with your limits, which becomes your status line: `Sem 12% (reset seg 09:00)`.

The widget watches that file and updates when it changes.

### I already have a status line

Add one line at the top of your script to pass the JSON to ClaudeWatch, then keep using `$input` instead of stdin:

```bash
input=$(cat); printf '%s' "$input" | "C:/Users/you/Apps/ClaudeWatch.exe" --statusline-capture
```

In PowerShell:

```powershell
$raw = [Console]::In.ReadToEnd(); $raw | & "C:/Users/you/Apps/ClaudeWatch.exe" --statusline-capture
$in = $raw | ConvertFrom-Json
```

`--statusline-capture` only writes the file and prints nothing. The **Fonte dos dados → Configurar a statusline** menu detects this case and copies the right line to your clipboard.

### Limitations

- **No Fable limit**, and no other per-model limits. For those, only the usage API.
- **Updates only while you use Claude Code.** When idle, the widget shows the last value and when it arrived.
- **Data comes from Claude Code in the terminal** (`claude`). In the VS Code extension panel, the status line was not called in our test (Nov 2026). If the widget stays on "Aguardando o Claude Code", open a `claude` session in a terminal and send a message: the limits arrive with the first response.
- **Claude Code inside WSL** uses a different `settings.json`. There the setup is manual.
- **If you move the `.exe`**, ClaudeWatch fixes the path the next time it starts.

## Source 2 — the usage API (unofficial)

> ⚠️ **Undocumented and unguaranteed.** It's the same response behind Claude Code's `/usage` command. ClaudeWatch calls it with the token Claude Code stores, and [Anthropic's terms](https://code.claude.com/docs/en/legal-and-compliance) restrict using that token outside Claude Code. Use it knowing that. This section documents **the response format**, for anyone who needs to interpret it, and doesn't explain how to obtain the token.

### `limits`: the plan's limits

This is the part that matters. Each item is a limit that exists on your plan:

```json
"limits": [
  { "kind": "weekly_all",    "group": "weekly", "percent": 12, "severity": "normal",
    "resets_at": "2026-11-30T12:00:00.175587+00:00", "scope": null, "is_active": false },
  { "kind": "weekly_scoped", "group": "weekly", "percent": 18, "severity": "normal",
    "resets_at": "2026-11-30T12:00:00.175809+00:00",
    "scope": { "model": { "id": null, "display_name": "Fable" }, "surface": null }, "is_active": true }
]
```

| Field | Meaning |
|---|---|
| `kind` | Kind of limit. Seen so far: `weekly_all` (week, all models) and `weekly_scoped` (week, one model) |
| `group` | The limit's window. Seen so far: `weekly` |
| `percent` | Usage, 0 to 100 |
| `resets_at` | When it resets, in **ISO 8601** (the status line uses epoch) |
| `scope.model.display_name` | The model of a scoped limit: `"Fable"` |
| `severity` | `"normal"` in the observed data; likely changes near the limit |
| `is_active` | `true` on the limit that seems to be the binding one right now. Still being investigated ([#13](https://github.com/carlosdealmeida/claude-watch/issues/13)) |

ClaudeWatch reads the items in this order: session, week, per-model limits. Any unknown `kind` shows up under its own name, so a plan change doesn't break the widget.

### `weekly_scoped_shares`: how much of the week Fable has used

```json
"weekly_scoped_shares": [ { "limit_index": 1, "used_percent_of_weekly": 9, "allowance_percent_of_weekly": 50 } ]
```

On Max and Ultra, Fable can use up to **50% of the week**, and what it uses comes out of the same week as every other model. `limit_index` points to the scoped item in `limits`; `used_percent_of_weekly` is the share of the week it has already used.

In the example the week is at 12%, and 9 of those points came from Fable. The **Barras** (Bars) style draws that share hatched inside the weekly bar.

### `seven_day_breakdown`: where usage came from

```json
"seven_day_breakdown": {
  "as_of": "2026-11-24T12:31:43+00:00",
  "window_started_at": "2026-11-23T12:00:00+00:00",
  "rows": [
    { "key": "claude_code", "display_name": "Claude Code", "percent": 92, "percent_of_limit": 11 },
    { "key": "cowork",      "display_name": "Cowork",      "percent": 8,  "percent_of_limit": 1 }
  ]
}
```

`percent` is the part of this week's usage that came from that surface; `percent_of_limit` is how much of the limit it accounts for. `window_started_at` is the **start of the weekly window**, which the pace calculation uses.

### The legacy fields

Before `limits`, the response had fixed buckets, and they still come along:

```json
"five_hour": null,
"seven_day": { "utilization": 12.0, "resets_at": "2026-11-30T12:00:00.175587+00:00" },
"seven_day_sonnet": null,
"seven_day_opus": null
```

A `null` bucket means the limit doesn't exist on your plan. ClaudeWatch only uses these fields when `limits` is missing. The response also has dozens of other fields, almost all null; they stay off this page until they carry real data.

## Pace: when the limit runs out

Knowing you're at 18% doesn't tell you whether you'll make it to the end of the week. With the window start and the reset time you can project it, without storing any history:

```
elapsed   = now − window_start
runs_out  = now + elapsed × (100 − usage) / usage
```

This assumes you keep the same average pace as the window so far. If `runs_out` lands **before** the reset, the limit runs out first, and ClaudeWatch shows `⚠ acaba dom 01:16` (runs out Sun 01:16) in amber. Otherwise it shows when it resets: `↻ seg 09:00` (Mon 09:00).

**A real example.** The week started on Monday at 09:00 and it's now Tuesday at 09:31, so 24.5 h have passed. Fable is at 18%:

```
runs_out = Tue 09:31 + 24.5 h × 82 / 18 ≈ Tue 09:31 + 111.6 h = Sun 01:16
```

The reset is Monday at 09:00, so Fable runs out about 32 hours early. The week, at 12% over the same period, would only reach 100% the following Tuesday, after the reset.

Two safeguards keep the projection honest:

- **Early in the window it's noise.** One big prompt in the first hour projects the end of the week for tomorrow. ClaudeWatch only projects after **4% of the window**: about 7 hours of a week, 12 minutes of a 5-hour session.
- **Below 2% usage** it doesn't project either, for the same reason.

**Where the window start comes from:**
- From the API: `seven_day_breakdown.window_started_at`.
- The status line has no such field, so ClaudeWatch uses `resets_at − 7 days` (or `− 5 hours` for the session).

### Recipe: a status line with pace, in bash

Only the official source plus `jq`. On macOS, replace `date -d @$t` with `date -r $t`.

```bash
#!/usr/bin/env bash
input=$(cat)
pct=$(jq -r '.rate_limits.seven_day.used_percentage // empty' <<<"$input")
reset=$(jq -r '.rate_limits.seven_day.resets_at // empty' <<<"$input")
[ -z "$pct" ] && { echo "week: —"; exit 0; }

now=$(date +%s); start=$((reset - 604800)); elapsed=$((now - start))
eta=$(awk -v p="$pct" -v e="$elapsed" -v n="$now" \
  'BEGIN { if (p < 2 || e < 0.04 * 604800) print ""; else printf "%d", n + e * (100 - p) / p }')

if [ -n "$eta" ] && [ "$eta" -lt "$reset" ]; then
  printf 'week %.0f%% ⚠ runs out %s\n' "$pct" "$(date -d @"$eta" '+%a %H:%M')"
else
  printf 'week %.0f%% ↻ %s\n' "$pct" "$(date -d @"$reset" '+%a %H:%M')"
fi
```

### Recipe: the same in PowerShell

Configure it with `"command": "powershell -NoProfile -File C:/path/statusline.ps1"`.

```powershell
$in = [Console]::In.ReadToEnd() | ConvertFrom-Json
$w = $in.rate_limits.seven_day
if (-not $w) { 'week: —'; return }

$now = [DateTimeOffset]::UtcNow.ToUnixTimeSeconds()
$elapsed = $now - ($w.resets_at - 604800)
$p = $w.used_percentage
$local = { param($t) [DateTimeOffset]::FromUnixTimeSeconds([long]$t).ToLocalTime() }

if ($p -ge 2 -and $elapsed -ge 0.04 * 604800) {
  $eta = $now + $elapsed * (100 - $p) / $p
  if ($eta -lt $w.resets_at) { 'week {0:N0}% ⚠ runs out {1:ddd HH:mm}' -f $p, (& $local $eta); return }
}
'week {0:N0}% ↻ {1:ddd HH:mm}' -f $p, (& $local $w.resets_at)
```

## What has changed

| When | What changed |
|---|---|
| 2026 | Claude Code started passing `rate_limits` (5h and week) to status line scripts |
| Until Oct 2026 | The API only had fixed buckets: `five_hour`, `seven_day`, `seven_day_sonnet`, `seven_day_opus` |
| Nov 2026 | Max 20x became **Ultra**: verified accounts lost the 5-hour session and kept the week plus a weekly Fable limit. The API gained `limits`, `weekly_scoped_shares` and `seven_day_breakdown` |

Seeing something different on your account? Open an [issue](https://github.com/carlosdealmeida/claude-watch/issues) with the response, without tokens or anything personal.

## References

- [Claude Code — Customize your status line](https://code.claude.com/docs/en/statusline): the `rate_limits` fields.
- [Claude Code — Legal and compliance](https://code.claude.com/docs/en/legal-and-compliance): using the subscription token.
- [Fable on your plan](https://support.claude.com/en/articles/15424964): up to 50% of the weekly limit.
