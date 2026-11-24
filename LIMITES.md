# Como o ClaudeWatch lê seus limites

<p><b>Português</b> · <a href="LIMITS.en.md">English</a> · <a href="LIMITES.es.md">Español</a></p>

A Anthropic não documenta uma API pública de "quanto da minha assinatura eu já usei". Esta página reúne o que o ClaudeWatch usa para mostrar seus limites: **de onde vêm os dados, o que cada campo significa e como calcular o ritmo**. Assim você usa o widget (o peixe) e consegue montar a sua própria ferramenta (a vara).

> Os formatos aqui foram **observados em 24/11/2026**. A fonte oficial tem documentação; a outra não, e pode mudar sem aviso. Cada seção diz qual é qual.

## As duas fontes

| | **Statusline do Claude Code** (padrão) | **API de uso** (opt-in) |
|---|---|---|
| Status | Oficial e documentada | Não oficial, sem documentação |
| Traz | Sessão de 5h e semana | Tudo: sessão, semana, **limite do Fable**, fatia do Fable na semana, de onde veio o consumo |
| Atualiza | A cada resposta do Claude Code | Quando o ClaudeWatch consulta (a cada minuto) |
| Precisa de | Uma sessão do Claude Code em uso | O token do Claude Code |
| Risco | Nenhum | O endpoint pode mudar ou parar; os [termos](https://code.claude.com/docs/en/legal-and-compliance) restringem o uso do token fora do Claude Code |

A fonte se escolhe na primeira execução e depois no menu do ícone, em **Fonte dos dados**.

## Fonte 1 — a statusline do Claude Code (oficial)

O Claude Code roda um comando seu como [statusline](https://code.claude.com/docs/en/statusline) e passa no stdin um JSON com o estado da sessão. Para assinantes, esse JSON traz `rate_limits`:

```json
"rate_limits": {
  "five_hour": { "used_percentage": 23.5, "resets_at": 1795528800 },
  "seven_day": { "used_percentage": 12,   "resets_at": 1796040000 }
}
```

| Campo | Significado |
|---|---|
| `used_percentage` | Quanto da janela já foi usado, de 0 a 100 |
| `resets_at` | Quando a janela reseta, em **segundos Unix** (epoch) |

Regras da documentação oficial que importam para quem lê esses dados:

- `rate_limits` só aparece **depois da primeira resposta** da sessão.
- Cada janela pode faltar sozinha. No plano Ultra verificado não existe `five_hour`.
- Quando o `resets_at` de uma janela passa, o Claude Code descarta essa janela.
- Atrás de um gateway com limite de gasto, aparece também `spend_limit`. O ClaudeWatch o ignora.

### O que o ClaudeWatch faz com isso

Na fonte oficial, o ClaudeWatch configura a si mesmo como statusline em `~/.claude/settings.json`:

```json
"statusLine": { "type": "command", "command": "\"C:/Users/voce/Apps/ClaudeWatch.exe\" --statusline" }
```

Antes de mexer no arquivo ele guarda um backup em `settings.json.claudewatch.bak`, preserva todo o resto e **nunca substitui uma statusline que já exista**.

Chamado pelo Claude Code, o `ClaudeWatch.exe --statusline` faz duas coisas e sai:

1. Grava **só os limites** em `%APPDATA%\ClaudeWatch\statusline.json`. Diretório, sessão e modelo ficam de fora:
   ```json
   { "captured_at": "2026-11-24T09:31:43-03:00", "rate_limits": { "seven_day": { "used_percentage": 12, "resets_at": 1796040000 } } }
   ```
2. Imprime uma linha com os limites, que vira a sua statusline: `Sem 12% (reset seg 09:00)`.

O widget observa esse arquivo e se atualiza quando ele muda.

### Já tenho uma statusline

Coloque uma linha no começo do seu script, para repassar o JSON ao ClaudeWatch, e continue usando `$input` no lugar do stdin:

```bash
input=$(cat); printf '%s' "$input" | "C:/Users/voce/Apps/ClaudeWatch.exe" --statusline-capture
```

No PowerShell:

```powershell
$raw = [Console]::In.ReadToEnd(); $raw | & "C:/Users/voce/Apps/ClaudeWatch.exe" --statusline-capture
$in = $raw | ConvertFrom-Json
```

`--statusline-capture` só grava o arquivo e não imprime nada. O menu **Fonte dos dados → Configurar a statusline** detecta esse caso e copia a linha certa para a área de transferência.

### Limitações

- **Não traz o limite do Fable** nem outros limites por modelo. Para eles, só a API de uso.
- **Só atualiza enquanto você usa o Claude Code.** Parado, o widget mostra o último valor e o horário em que ele chegou.
- **O Claude Code dentro do WSL** usa outro `settings.json`. Nesse caso a configuração é manual.
- **Se você mover o `.exe`**, o ClaudeWatch corrige o caminho na próxima vez que abrir.

## Fonte 2 — a API de uso (não oficial)

> ⚠️ **Sem documentação e sem garantia.** É a mesma resposta que alimenta o comando `/usage` do Claude Code. O ClaudeWatch a consulta com o token que o Claude Code guarda, e os [termos da Anthropic](https://code.claude.com/docs/en/legal-and-compliance) restringem o uso desse token fora do Claude Code. Use sabendo disso. Esta seção documenta **o formato da resposta**, para quem precisa interpretá-la, e não ensina a obter o token.

### `limits`: a lista de limites do plano

É a parte que importa. Cada item é um limite que existe no seu plano:

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
| `kind` | Tipo do limite. Observados: `weekly_all` (semana, todos os modelos) e `weekly_scoped` (semana de um modelo) |
| `group` | Janela do limite. Observado: `weekly` |
| `percent` | Uso, de 0 a 100 |
| `resets_at` | Quando reseta, em **ISO 8601** (na statusline é epoch) |
| `scope.model.display_name` | O modelo do limite com escopo: `"Fable"` |
| `severity` | `"normal"` nos dados observados; deve mudar perto do limite |
| `is_active` | `true` no limite que parece estar restringindo agora. Ainda em observação ([#13](https://github.com/carlosdealmeida/claude-watch/issues/13)) |

O ClaudeWatch lê os itens nesta ordem: sessão, semana, limites por modelo, e qualquer `kind` desconhecido aparece com o próprio nome. Assim uma mudança de plano não quebra o widget.

### `weekly_scoped_shares`: quanto o Fable já comeu da semana

```json
"weekly_scoped_shares": [ { "limit_index": 1, "used_percent_of_weekly": 9, "allowance_percent_of_weekly": 50 } ]
```

No Max e no Ultra o Fable pode usar até **50% da semana**, e o que ele consome sai da mesma semana dos outros modelos. `limit_index` aponta para o item de `limits` com escopo; `used_percent_of_weekly` é a fatia da semana que ele já consumiu.

No exemplo, a semana está em 12%, dos quais 9 pontos vieram do Fable. O estilo **Barras** desenha essa fatia hachurada dentro da barra da semana.

### `seven_day_breakdown`: de onde veio o consumo

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

`percent` é a parte do consumo da semana que veio daquela superfície; `percent_of_limit` é quanto ela representa do limite. `window_started_at` é o **início da janela semanal**, que o cálculo do ritmo usa.

### Os campos antigos

Antes de `limits`, a resposta tinha buckets fixos, e eles continuam vindo:

```json
"five_hour": null,
"seven_day": { "utilization": 12.0, "resets_at": "2026-11-30T12:00:00.175587+00:00" },
"seven_day_sonnet": null,
"seven_day_opus": null
```

Bucket `null` significa que o limite não existe no seu plano. O ClaudeWatch só usa esses campos quando `limits` não vem. A resposta tem ainda dezenas de outros campos, quase todos nulos; eles ficam fora desta página até mostrarem dado de verdade.

## O ritmo: quando o limite acaba

Saber que você está em 18% não diz se dá para chegar ao fim da semana. Com o início da janela e o reset, dá para projetar, sem guardar histórico:

```
decorrido = agora − início_da_janela
acaba_em  = agora + decorrido × (100 − uso) / uso
```

A conta supõe que você continua no mesmo ritmo médio da janela até agora. Se `acaba_em` cai **antes** do reset, o limite acaba antes, e o ClaudeWatch mostra `⚠ acaba dom 01:16` em âmbar. Se cai depois, mostra quando reseta: `↻ seg 09:00`.

**Exemplo real.** A semana começou na segunda às 09:00 e agora é terça às 09:31, ou seja, 24,5 h decorridas. O Fable está em 18%:

```
acaba_em = terça 09:31 + 24,5 h × 82 / 18 ≈ terça 09:31 + 111,6 h = domingo 01:16
```

O reset é segunda às 09:00, então o Fable acaba cerca de 32 horas antes. A semana, com 12% no mesmo período, projetaria 100% só na terça seguinte, depois do reset.

Dois cuidados para a conta não mentir:

- **No começo da janela ela é ruído.** Um prompt grande na primeira hora projeta o fim da semana para amanhã. O ClaudeWatch só projeta depois de **4% da janela**: cerca de 7 horas numa semana, 12 minutos numa sessão de 5h.
- **Abaixo de 2% de uso** também não projeta, pelo mesmo motivo.

**De onde vem o início da janela:**
- Na API: `seven_day_breakdown.window_started_at`.
- Na statusline não há esse campo, então o ClaudeWatch usa `resets_at − 7 dias` (ou `− 5 horas` na sessão).

### Receita: uma statusline com ritmo, em bash

Só a fonte oficial e `jq`. Em macOS, troque `date -d @$t` por `date -r $t`.

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
  printf 'semana %.0f%% ⚠ acaba %s\n' "$pct" "$(date -d @"$eta" '+%a %H:%M')"
else
  printf 'semana %.0f%% ↻ %s\n' "$pct" "$(date -d @"$reset" '+%a %H:%M')"
fi
```

### Receita: a mesma coisa em PowerShell

Configure com `"command": "powershell -NoProfile -File C:/caminho/statusline.ps1"`.

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
  if ($eta -lt $w.resets_at) { 'semana {0:N0}% ⚠ acaba {1:ddd HH:mm}' -f $p, (& $local $eta); return }
}
'semana {0:N0}% ↻ {1:ddd HH:mm}' -f $p, (& $local $w.resets_at)
```

## O que já mudou

| Quando | O que mudou |
|---|---|
| 2026 | O Claude Code passou a entregar `rate_limits` (5h e semana) para scripts de statusline |
| Até out/2026 | A API trazia só buckets fixos: `five_hour`, `seven_day`, `seven_day_sonnet`, `seven_day_opus` |
| Nov/2026 | O Max 20x virou **Ultra**: contas verificadas perderam a sessão de 5h e ficaram com semana + limite semanal do Fable. A API ganhou `limits`, `weekly_scoped_shares` e `seven_day_breakdown` |

Viu algo diferente na sua conta? Abra uma [issue](https://github.com/carlosdealmeida/claude-watch/issues) com a resposta, sem tokens e sem nada pessoal.

## Referências

- [Claude Code — Customize your status line](https://code.claude.com/docs/en/statusline): os campos de `rate_limits`.
- [Claude Code — Legal and compliance](https://code.claude.com/docs/en/legal-and-compliance): uso do token da assinatura.
- [Fable no seu plano](https://support.claude.com/en/articles/15424964): até 50% do limite semanal.
