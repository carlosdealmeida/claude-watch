# Cómo lee ClaudeWatch tus límites

<p><a href="LIMITES.md">Português</a> · <a href="LIMITS.en.md">English</a> · <b>Español</b></p>

Anthropic no documenta una API pública de "cuánto de mi suscripción he usado". Esta página reúne lo que ClaudeWatch usa para mostrar tus límites: **de dónde vienen los datos, qué significa cada campo y cómo calcular el ritmo**. Así usas el widget (el pez) y puedes armar tu propia herramienta (la caña).

> Los formatos de aquí se **observaron el 24/11/2026**. La fuente oficial está documentada; la otra no, y puede cambiar sin aviso. Cada sección dice cuál es cuál.

## Las dos fuentes

| | **Statusline de Claude Code** (por defecto) | **API de uso** (opcional) |
|---|---|---|
| Estado | Oficial y documentada | No oficial, sin documentación |
| Trae | Sesión de 5h y semana | Todo: sesión, semana, **límite de Fable**, parte de Fable en la semana, de dónde vino el consumo |
| Se actualiza | En cada respuesta de Claude Code | Cuando ClaudeWatch consulta (cada minuto) |
| Necesita | Una sesión de Claude Code en uso | El token de Claude Code |
| Riesgo | Ninguno | El endpoint puede cambiar o dejar de funcionar; los [términos](https://code.claude.com/docs/en/legal-and-compliance) restringen el uso del token fuera de Claude Code |

La fuente se elige en la primera ejecución y después en el menú del icono, en **Fonte dos dados** (Fuente de datos).

## Fuente 1 — la statusline de Claude Code (oficial)

Claude Code ejecuta un comando tuyo como [statusline](https://code.claude.com/docs/en/statusline) y le pasa por stdin un JSON con el estado de la sesión. Para suscriptores, ese JSON incluye `rate_limits`:

```json
"rate_limits": {
  "five_hour": { "used_percentage": 23.5, "resets_at": 1795528800 },
  "seven_day": { "used_percentage": 12,   "resets_at": 1796040000 }
}
```

| Campo | Significado |
|---|---|
| `used_percentage` | Cuánto de la ventana se ha usado, de 0 a 100 |
| `resets_at` | Cuándo se reinicia la ventana, en **segundos Unix** (epoch) |

Reglas de la documentación oficial que importan al leer estos datos:

- `rate_limits` solo aparece **después de la primera respuesta** de la sesión.
- Cada ventana puede faltar por separado. En las cuentas Ultra verificadas no existe `five_hour`.
- Cuando pasa el `resets_at` de una ventana, Claude Code la descarta.
- Detrás de un gateway con límite de gasto aparece también `spend_limit`. ClaudeWatch lo ignora.

### Qué hace ClaudeWatch con esto

Con la fuente oficial, ClaudeWatch se configura a sí mismo como statusline en `~/.claude/settings.json`:

```json
"statusLine": { "type": "command", "command": "\"C:/Users/tu/Apps/ClaudeWatch.exe\" --statusline" }
```

Antes de tocar el archivo guarda una copia en `settings.json.claudewatch.bak`, conserva todo lo demás y **nunca reemplaza una statusline que ya exista**.

Cuando Claude Code lo llama, `ClaudeWatch.exe --statusline` hace dos cosas y termina:

1. Guarda **solo los límites** en `%APPDATA%\ClaudeWatch\statusline.json`. Directorio, sesión y modelo quedan fuera:
   ```json
   { "captured_at": "2026-11-24T09:31:43-03:00", "rate_limits": { "seven_day": { "used_percentage": 12, "resets_at": 1796040000 } } }
   ```
2. Imprime una línea con tus límites, que se convierte en tu statusline: `Sem 12% (reset seg 09:00)`.

El widget vigila ese archivo y se actualiza cuando cambia.

### Ya tengo una statusline

Agrega una línea al principio de tu script para pasarle el JSON a ClaudeWatch, y sigue usando `$input` en lugar de stdin:

```bash
input=$(cat); printf '%s' "$input" | "C:/Users/tu/Apps/ClaudeWatch.exe" --statusline-capture
```

En PowerShell:

```powershell
$raw = [Console]::In.ReadToEnd(); $raw | & "C:/Users/tu/Apps/ClaudeWatch.exe" --statusline-capture
$in = $raw | ConvertFrom-Json
```

`--statusline-capture` solo guarda el archivo y no imprime nada. El menú **Fonte dos dados → Configurar a statusline** detecta este caso y copia la línea correcta al portapapeles.

### Limitaciones

- **No trae el límite de Fable** ni otros límites por modelo. Para eso, solo la API de uso.
- **Solo se actualiza mientras usas Claude Code.** Sin uso, el widget muestra el último valor y la hora en que llegó.
- **Claude Code dentro de WSL** usa otro `settings.json`. Ahí la configuración es manual.
- **Si mueves el `.exe`**, ClaudeWatch corrige la ruta la próxima vez que se abra.

## Fuente 2 — la API de uso (no oficial)

> ⚠️ **Sin documentación y sin garantía.** Es la misma respuesta que alimenta el comando `/usage` de Claude Code. ClaudeWatch la consulta con el token que Claude Code guarda, y los [términos de Anthropic](https://code.claude.com/docs/en/legal-and-compliance) restringen el uso de ese token fuera de Claude Code. Úsala sabiéndolo. Esta sección documenta **el formato de la respuesta**, para quien necesite interpretarla, y no explica cómo obtener el token.

### `limits`: la lista de límites del plan

Es la parte que importa. Cada elemento es un límite que existe en tu plan:

```json
"limits": [
  { "kind": "weekly_all",    "group": "weekly", "percent": 12, "severity": "normal",
    "resets_at": "2026-11-30T12:00:00.175587+00:00", "scope": null, "is_active": false },
  { "kind": "weekly_scoped", "group": "weekly", "percent": 18, "severity": "normal",
    "resets_at": "2026-11-30T12:00:00.175809+00:00",
    "scope": { "model": { "id": null, "display_name": "Fable" }, "surface": null }, "is_active": true }
]
```

| Campo | Significado |
|---|---|
| `kind` | Tipo de límite. Observados: `weekly_all` (semana, todos los modelos) y `weekly_scoped` (semana de un modelo) |
| `group` | Ventana del límite. Observado: `weekly` |
| `percent` | Uso, de 0 a 100 |
| `resets_at` | Cuándo se reinicia, en **ISO 8601** (en la statusline es epoch) |
| `scope.model.display_name` | El modelo de un límite con alcance: `"Fable"` |
| `severity` | `"normal"` en los datos observados; seguramente cambia cerca del límite |
| `is_active` | `true` en el límite que parece estar restringiendo ahora. Aún en estudio ([#13](https://github.com/carlosdealmeida/claude-watch/issues/13)) |

ClaudeWatch lee los elementos en este orden: sesión, semana, límites por modelo. Cualquier `kind` desconocido aparece con su propio nombre, así que un cambio de plan no rompe el widget.

### `weekly_scoped_shares`: cuánto de la semana ya consumió Fable

```json
"weekly_scoped_shares": [ { "limit_index": 1, "used_percent_of_weekly": 9, "allowance_percent_of_weekly": 50 } ]
```

En Max y Ultra, Fable puede usar hasta el **50% de la semana**, y lo que consume sale de la misma semana que los demás modelos. `limit_index` apunta al elemento con alcance de `limits`; `used_percent_of_weekly` es la parte de la semana que ya consumió.

En el ejemplo, la semana está al 12%, y 9 de esos puntos vinieron de Fable. El estilo **Barras** dibuja esa parte rayada dentro de la barra de la semana.

### `seven_day_breakdown`: de dónde vino el consumo

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

`percent` es la parte del consumo de la semana que vino de esa superficie; `percent_of_limit` es cuánto representa del límite. `window_started_at` es el **inicio de la ventana semanal**, que usa el cálculo del ritmo.

### Los campos antiguos

Antes de `limits`, la respuesta tenía buckets fijos, y siguen llegando:

```json
"five_hour": null,
"seven_day": { "utilization": 12.0, "resets_at": "2026-11-30T12:00:00.175587+00:00" },
"seven_day_sonnet": null,
"seven_day_opus": null
```

Un bucket `null` significa que el límite no existe en tu plan. ClaudeWatch solo usa estos campos cuando no viene `limits`. La respuesta tiene además decenas de otros campos, casi todos nulos; quedan fuera de esta página hasta que traigan datos reales.

## El ritmo: cuándo se agota el límite

Saber que estás al 18% no dice si llegas al final de la semana. Con el inicio de la ventana y el reinicio se puede proyectar, sin guardar historial:

```
transcurrido = ahora − inicio_de_la_ventana
se_agota     = ahora + transcurrido × (100 − uso) / uso
```

La cuenta supone que sigues al mismo ritmo medio de la ventana hasta ahora. Si `se_agota` cae **antes** del reinicio, el límite se agota antes, y ClaudeWatch muestra `⚠ acaba dom 01:16` (se agota el dom 01:16) en ámbar. Si cae después, muestra cuándo se reinicia: `↻ seg 09:00` (lun 09:00).

**Ejemplo real.** La semana empezó el lunes a las 09:00 y ahora es martes a las 09:31: han pasado 24,5 h. Fable está al 18%:

```
se_agota = mar 09:31 + 24,5 h × 82 / 18 ≈ mar 09:31 + 111,6 h = dom 01:16
```

El reinicio es el lunes a las 09:00, así que Fable se agota unas 32 horas antes. La semana, con 12% en el mismo periodo, llegaría al 100% recién el martes siguiente, después del reinicio.

Dos cuidados para que la cuenta no engañe:

- **Al principio de la ventana es ruido.** Un prompt grande en la primera hora proyecta el fin de la semana para mañana. ClaudeWatch solo proyecta después del **4% de la ventana**: unas 7 horas de una semana, 12 minutos de una sesión de 5h.
- **Por debajo del 2% de uso** tampoco proyecta, por el mismo motivo.

**De dónde sale el inicio de la ventana:**
- En la API: `seven_day_breakdown.window_started_at`.
- La statusline no tiene ese campo, así que ClaudeWatch usa `resets_at − 7 días` (o `− 5 horas` para la sesión).

### Receta: una statusline con ritmo, en bash

Solo la fuente oficial y `jq`. En macOS, cambia `date -d @$t` por `date -r $t`.

```bash
#!/usr/bin/env bash
input=$(cat)
pct=$(jq -r '.rate_limits.seven_day.used_percentage // empty' <<<"$input")
reset=$(jq -r '.rate_limits.seven_day.resets_at // empty' <<<"$input")
[ -z "$pct" ] && { echo "semana: —"; exit 0; }

now=$(date +%s); start=$((reset - 604800)); elapsed=$((now - start))
eta=$(awk -v p="$pct" -v e="$elapsed" -v n="$now" \
  'BEGIN { if (p < 2 || e < 0.04 * 604800) print ""; else printf "%d", n + e * (100 - p) / p }')

if [ -n "$eta" ] && [ "$eta" -lt "$reset" ]; then
  printf 'semana %.0f%% ⚠ se agota %s\n' "$pct" "$(date -d @"$eta" '+%a %H:%M')"
else
  printf 'semana %.0f%% ↻ %s\n' "$pct" "$(date -d @"$reset" '+%a %H:%M')"
fi
```

### Receta: lo mismo en PowerShell

Configúralo con `"command": "powershell -NoProfile -File C:/ruta/statusline.ps1"`.

```powershell
$in = [Console]::In.ReadToEnd() | ConvertFrom-Json
$w = $in.rate_limits.seven_day
if (-not $w) { 'semana: —'; return }

$now = [DateTimeOffset]::UtcNow.ToUnixTimeSeconds()
$elapsed = $now - ($w.resets_at - 604800)
$p = $w.used_percentage
$local = { param($t) [DateTimeOffset]::FromUnixTimeSeconds([long]$t).ToLocalTime() }

if ($p -ge 2 -and $elapsed -ge 0.04 * 604800) {
  $eta = $now + $elapsed * (100 - $p) / $p
  if ($eta -lt $w.resets_at) { 'semana {0:N0}% ⚠ se agota {1:ddd HH:mm}' -f $p, (& $local $eta); return }
}
'semana {0:N0}% ↻ {1:ddd HH:mm}' -f $p, (& $local $w.resets_at)
```

## Qué ha cambiado

| Cuándo | Qué cambió |
|---|---|
| 2026 | Claude Code empezó a pasar `rate_limits` (5h y semana) a los scripts de statusline |
| Hasta oct/2026 | La API solo tenía buckets fijos: `five_hour`, `seven_day`, `seven_day_sonnet`, `seven_day_opus` |
| Nov/2026 | Max 20x pasó a ser **Ultra**: las cuentas verificadas perdieron la sesión de 5h y quedaron con la semana más un límite semanal de Fable. La API ganó `limits`, `weekly_scoped_shares` y `seven_day_breakdown` |

¿Ves algo distinto en tu cuenta? Abre un [issue](https://github.com/carlosdealmeida/claude-watch/issues) con la respuesta, sin tokens ni nada personal.

## Referencias

- [Claude Code — Customize your status line](https://code.claude.com/docs/en/statusline): los campos de `rate_limits`.
- [Claude Code — Legal and compliance](https://code.claude.com/docs/en/legal-and-compliance): uso del token de la suscripción.
- [Fable en tu plan](https://support.claude.com/en/articles/15424964): hasta el 50% del límite semanal.
