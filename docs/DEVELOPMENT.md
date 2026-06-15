# Desenvolvimento — ClaudeWatch

Documentação técnica para compilar, testar, publicar e lançar o ClaudeWatch.

## Arquitetura

Núcleo puro (`Core/`, `Credentials/`) isolado de UI e I/O por abstrações pequenas.

- **`CredentialPipeline`** lê `~/.claude/.credentials.json` (**read-only, invariante
  absoluta — nunca escreve no arquivo do Claude Code**), renova com refresh token e
  cacheia em DPAPI (`token.bin`).
- **`UsagePoller`** publica um `UsageSnapshot` imutável (estados `Ok` / `Stale` /
  `NoCredential`) para a janela (dual-skin) e o tray, com backoff 1→2→5 min.
- **`UpdateService`** + **`GitHubReleaseClient`** + **`UpdateChecker`** verificam a
  última release no GitHub e avisam (notify-only) por tray/balão/widget.
- **`WidgetWindow`** (WPF borderless topmost) + **`TrayController`** (WinForms NotifyIcon)
  no mesmo processo.

Decisões de design: **[2026-06-11-claudewatch-widget-design.md](2026-06-11-claudewatch-widget-design.md)** ·
pipeline + auto-update: **[superpowers/specs/2026-06-12-release-pipeline-autoupdate-design.md](superpowers/specs/2026-06-12-release-pipeline-autoupdate-design.md)**.

## Stack

`net10.0-windows` · WPF + WinForms (`UseWPF` + `UseWindowsForms`) · System.Text.Json ·
DPAPI (`System.Security.Cryptography.ProtectedData`, já no framework) · xUnit.

## Compilar e testar

```bash
dotnet build ClaudeWatch.slnx        # compila (0 warnings)
dotnet test  ClaudeWatch.slnx        # 60 testes
```

## Modo showroom (QA visual sem rede)

Injeta o snapshot canônico 42/78/96 sem chamar a API — útil para inspecionar os skins
e estados:

```powershell
$env:CLAUDEWATCH_SHOWROOM = "ok"      # ou "stale" | "nocred"
dotnet run --project src/ClaudeWatch
Remove-Item Env:\CLAUDEWATCH_SHOWROOM
```

## Publicar localmente (`.exe` único)

```powershell
./publish.ps1                         # -> publish/ClaudeWatch.exe (~180 MB)
```

Self-contained, `PublishSingleFile`, `ReadyToRun`, **`PublishTrimmed=false`**
(WPF quebra trimado — inegociável). Exe não assinado: aviso SmartScreen esperado.

## Lançar uma release

A versão vem da tag. O pipeline (`.github/workflows/release.yml`) roda em `windows-latest`
a cada tag `v*`: testa, publica o single-file e cria a GitHub Release com o `.exe` anexado.

```bash
# 1. bump da versão no csproj (<Version>)
# 2. commit
git tag v0.3.0
git push origin master --tags
```

## Notas de ambiente

- O .NET 10 gera **`ClaudeWatch.slnx`** (formato XML de solução), não `.sln`.
- O SDK WindowsDesktop usa usings implícitos reduzidos; `System.IO` e `System.Net.Http`
  foram reincluídos no csproj, e `System.Windows.Forms`/`System.Drawing` foram removidos
  dos implícitos (colidiam com `System.Windows`).
- `ProtectedData` (DPAPI) já vem no framework `net10.0-windows` — sem `PackageReference`.
- Endpoints/constantes do OAuth e usage: ver **[TODO-carlos.md](TODO-carlos.md)**.
