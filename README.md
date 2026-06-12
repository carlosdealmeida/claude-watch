# ClaudeWatch

Widget de mesa para Windows (WPF) — sempre visível + ícone de tray — que mostra o
consumo da assinatura Claude (**sessão 5h**, **semana**, **Opus semanal**), lendo as
credenciais do **Claude Code oficial**. Processo único, distribuível como um `.exe`.

> Para usuários finais, veja **[README-amigos.md](README-amigos.md)**.

## Status

- ✅ Núcleo testável completo — **43 testes xUnit** verdes (zonas, snapshot, LED,
  tooltip, backoff, settings, parser/seleção/cache/refresh de credenciais, pipeline,
  usage, poller).
- ✅ UI completa — widget glass topmost, dois skins (Anéis/LED), tray com ícone GDI
  dinâmico, estados Stale/NoCredential, single-instance, autostart.
- ✅ **Endpoints reais preenchidos** (client_id, `platform.claude.com/v1/oauth/token`,
  `api.anthropic.com/api/oauth/usage`, grant form-urlencoded, headers `anthropic-beta`) —
  da fonte primária (Claude Code CLI descompilado em `claude-code-dotnet`/ATLAS). Ver
  **[docs/TODO-carlos.md](docs/TODO-carlos.md)**.
- ⏳ Falta apenas o **smoke contra a API real** (chamada autenticada com o token do
  Claude Code logado) para confirmar o 200 e os três medidores ao vivo.

## Arquitetura

Núcleo puro (`Core/`, `Credentials/`) isolado de UI e I/O por abstrações pequenas.

- **`CredentialPipeline`** lê `~/.claude/.credentials.json` (**read-only, invariante
  absoluta — nunca escreve no arquivo do Claude Code**), renova com refresh token e
  cacheia em DPAPI (`token.bin`).
- **`UsagePoller`** publica um `UsageSnapshot` imutável (estados `Ok` / `Stale` /
  `NoCredential`) para a janela (dual-skin) e o tray, com backoff 1→2→5 min.
- **`WidgetWindow`** (WPF borderless topmost) + **`TrayController`** (WinForms NotifyIcon)
  no mesmo processo.

Decisões de design: **[docs/2026-06-11-claudewatch-widget-design.md](docs/2026-06-11-claudewatch-widget-design.md)**.
Plano de implementação task-a-task: **[docs/2026-06-11-claudewatch-implementation.md](docs/2026-06-11-claudewatch-implementation.md)**.

## Stack

`net10.0-windows` · WPF + WinForms (`UseWPF` + `UseWindowsForms`) · System.Text.Json ·
DPAPI (`System.Security.Cryptography.ProtectedData`, já no framework) · xUnit.

## Desenvolvimento

```bash
dotnet build ClaudeWatch.slnx        # compila (0 warnings)
dotnet test  ClaudeWatch.slnx        # 43 testes
```

### Modo showroom (QA visual sem rede)

Injeta o snapshot canônico 42/78/96 sem chamar a API — útil para inspecionar os skins
e estados enquanto os `TODO(carlos)` não estão preenchidos:

```powershell
$env:CLAUDEWATCH_SHOWROOM = "ok"      # ou "stale" | "nocred"
dotnet run --project src/ClaudeWatch
Remove-Item Env:\CLAUDEWATCH_SHOWROOM
```

### Publicar (`.exe` único)

```powershell
./publish.ps1                         # -> publish/ClaudeWatch.exe (~180 MB)
```

Self-contained, `PublishSingleFile`, `ReadyToRun`, **`PublishTrimmed=false`**
(WPF quebra trimado — inegociável). Exe não assinado: aviso SmartScreen esperado.

## Notas de implementação (desvios do ambiente)

- O .NET 10 gera **`ClaudeWatch.slnx`** (formato XML de solução), não `.sln`.
- O SDK WindowsDesktop usa usings implícitos reduzidos; `System.IO` e `System.Net.Http`
  foram reincluídos no csproj, e `System.Windows.Forms`/`System.Drawing` foram removidos
  dos implícitos (colidiam com `System.Windows`).
- `ProtectedData` (DPAPI) já vem no framework `net10.0-windows` — sem `PackageReference`.
