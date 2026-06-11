# ClaudeWatch — Widget de consumo da assinatura Claude

**Data:** 2026-06-11 · **Status:** design validado · **Destino sugerido:** `docs/plans/2026-06-11-claudewatch-widget-design.md`

Widget desktop Windows, sempre visível, com os três medidores de consumo da assinatura Claude (sessão 5h, semana, Opus semanal) + ícone de tray. Distribuível para terceiros: requisito é ter o Claude Code oficial instalado e logado. Standalone — nenhuma dependência do CORTEX em runtime.

---

## 1. Decisões registradas

| Decisão | Escolha | Alternativa rejeitada e motivo |
|---|---|---|
| UI | WPF (`net10.0-windows`) | Flutter (runtime pesado p/ 2 números); Blazor Hybrid (WebView2 ~100MB) |
| Forma | Widget flutuante topmost + NotifyIcon no mesmo processo | AppBar (rouba pixels permanentes); DeskBand (morto no Win11) |
| Fonte de credencial | `.credentials.json` do **Claude Code oficial** | Cortex.Server (`GET /usage`) — amarraria o widget ao CORTEX; credencial própria via PKCE (fricção de onboarding) |
| Política de refresh | Oportunista, **sem write-back** (refresh token é estável) | Write-back atômico — desnecessário com refresh estável; risco de corromper arquivo alheio |
| Skins | **Dois**, alternáveis: A (anéis) e E (LED) | — requisito do produto |
| Ícone do tray | Pior dos três, número bold na cor da faixa | Anel no ícone (ilegível a 16px); dois ícones (ruído) |

## 2. Arquitetura

Processo único, zero dependências externas além de `System.Security.Cryptography.ProtectedData` (first-party).

- **`WidgetWindow`** — janela borderless topmost; renderiza um `UsageSnapshot` via dual-skin.
- **`TrayIcon`** — `NotifyIcon` (WinForms via `UseWindowsForms` + `UseWPF` no csproj); ícone GDI dinâmico + menu de controle.
- **`UsagePoller`** — `PeriodicTimer`; produz `UsageSnapshot` imutável e publica para janela e tray.
- **`CredentialStore`** — leitura do `.claude` + cache DPAPI + refresh.
- **`Settings`** — `%AppData%\ClaudeWatch\settings.json`.

`UsageSnapshot` (imutável): `{ FiveHourPct, WeekPct, OpusPct, FiveHourReset, WeekReset, OpusReset, ColetadoEm, Estado }` onde `Estado ∈ { Ok, Stale, NoCredential }`.

## 3. Credenciais

**Leitura.** Caminho primário `%USERPROFILE%\.claude\.credentials.json`; fallback configurável para instalações via WSL (`\\wsl$\<distro>\home\<user>\.claude\.credentials.json`, campo `wslCredentialsPath` no settings). Parse tolerante: extrai `accessToken` / `refreshToken` / `expiresAt` ignorando campos desconhecidos; formato inesperado nunca crasha — degrada para `Stale` com log. `FileSystemWatcher` no arquivo recarrega em mudança (renovação, re-login, logout).

**O widget NUNCA escreve no arquivo do Claude Code.** Invariante absoluta.

**Cache próprio.** Access token renovado pelo widget vai para `%AppData%\ClaudeWatch\token.bin`, protegido com `ProtectedData` (escopo `CurrentUser`), contendo `{ accessToken, expiresAt }`.

**Seleção de token por chamada:** o access token válido com maior `expiresAt` entre arquivo e cache. Ambos expirados → refresh com o refresh token do arquivo (serializado por `SemaphoreSlim(1)`; single-instance já elimina concorrência entre processos do ClaudeWatch) → resultado só no cache.

**Logout/re-login.** Refresh recusado (4xx) → limpa cache → `Estado = NoCredential`. Watcher detecta re-login e o ciclo recomeça.

**Defesa contra rotação futura.** Se a resposta de refresh um dia trouxer `refresh_token` diferente do atual: usar o access recebido, logar warning, **não** tentar "consertar" o arquivo do CC. Mudança de política do servidor degrada o widget; jamais corrompe credencial alheia.

**TODO(carlos)** — copiar do parity do CORTEX: token endpoint + `client_id` público do fluxo OAuth; endpoint de usage (o mesmo que alimenta o `/usage` upstream) e shape da resposta.

## 4. WidgetWindow

XAML: `WindowStyle=None`, `AllowsTransparency=True`, fundo transparente, `Topmost`, `ShowInTaskbar=False`, `ResizeMode=NoResize`, `SizeToContent`. No `SourceInitialized`, via `SetWindowLongPtr(GWL_EXSTYLE)`: `WS_EX_TOOLWINDOW | WS_EX_NOACTIVATE` sempre; `WS_EX_TRANSPARENT | WS_EX_LAYERED` flipado em runtime pelo toggle *Travar* (click-through). Arraste por `DragMove()` quando destravado; posição persistida com clamp para a virtual screen no restore (monitor removido não pode parir widget fora da tela). Topmost reafirmado a cada tick do poller (mitiga janelas que roubam a z-order). DPI `PerMonitorV2`.

**Dual-skin:** dois `DataTemplate` (A e E) sobre o mesmo ViewModel, trocados por `ContentControl`; seleção no menu do tray, persistida no settings.

## 5. Design visual (canonizado pelo showroom)

**Tokens.** Verde `#3FB950` (<70) · Âmbar `#E8A23D` (70–89) · Vermelho `#F85149` (≥90). Glass: fundo `#CC1E1E1E`, raio 12px, borda 1px `rgba(255,255,255,.10)`, sombra `0 12px 34px rgba(0,0,0,.40)` + highlight interno `rgba(255,255,255,.07)`. Tipografia Segoe UI, números tabulares.

**Skin A — Três anéis.** Card 264×118, padding 14/14/12. Três unidades: anel 56px, stroke 6, linecap round, início no topo, trilha `rgba(255,255,255,.14)`, arco na cor da faixa do valor; % centrado (14px, w600); label abaixo (10.5px, branco 74%). Labels: `Sessão 5h · Semana · Opus`.

**Skin E — LED/equalizador.** Card 272×122. Três linhas em grid `48px | 1fr | 30px`: label (10.5px), 15 segmentos (h 11px, gap 3px, raio 1.5px), percentual à direita na cor da faixa. **Cor por posição:** o segmento `i` representa o centro `(i+0.5)/15·100`; acende se centro ≤ valor; sua cor vem da zona do *centro* (≥90 vermelho, ≥70 âmbar, senão verde) — a ponta da escala é sempre vermelha, o valor só decide quantos acendem. Apagado: `rgba(255,255,255,.10)`. Glow por segmento aceso (`0 0 5px cor` a ~67% alfa) — em WPF, resolver com borda/gradiente sutil se 45 `DropShadowEffect` pesarem.

**Estados (ambos os skins).** *Stale:* corpo com `grayscale(.78) saturate(.5) opacity(.6)` + rodapé "⚠ atualizado há X min". *NoCredential:* cadeado + "Faça login no Claude Code" + botão "Conectar conta" (ação: abrir página do Claude Code — URL a definir).

**Nota de fidelidade.** O `backdrop-filter: blur(20px)` do showroom não existe nativamente em WPF translúcido. Default: glass sem blur (a 80% de opacidade escura a perda é pequena). Acrylic real via `SetWindowCompositionAttribute` fica como flag experimental futura — API não documentada, histórico de lag no drag.

## 6. Tray

Ícone 32×32 redesenhado a cada snapshot: **pior dos três** em número bold, cor da faixa, fundo transparente, `TextRenderingHint.AntiAliasGridFit` (ClearType não renderiza sobre alpha). Ritual anti-leak obrigatório: `GetHicon()` → `Icon.FromHandle()` → `Clone()` → `DestroyIcon()` (P/Invoke), descartando o `Icon` anterior — pular isso vaza 1 GDI handle/refresh e estoura o limite de 10k em dias de uptime.

Tooltip (≤127 chars): `5h 42% (reset 15:00) · Sem 78% · Opus 96%`. Menu: *Mostrar/ocultar widget* · *Travar widget* ✓ · *Estilo: Anéis/LED* · *Atualizar agora* · *Iniciar com o Windows* ✓ · *Sair*. Duplo clique alterna o widget. Onboarding: lembrar que o Win11 esconde ícones novos no overflow.

## 7. Poller, resiliência, instância única

Tick (60s, configurável): seleciona token → refresh proativo se expira em <2min → GET usage → snapshot → publica. Rede fora: mantém último snapshot como `Stale`, backoff 1→2→5min. 401: pipeline de credencial (re-read → refresh → cache). Parse inesperado: `Stale` + log (`%AppData%\ClaudeWatch\logs`, rolling). Single-instance: mutex `Global\ClaudeWatch.Widget` + `EventWaitHandle` — segunda instância sinaliza "mostrar widget" e encerra.

## 8. Distribuição

`dotnet publish -c Release -r win-x64 --self-contained /p:PublishSingleFile=true /p:IncludeNativeLibrariesForSelfExtract=true /p:PublishReadyToRun=true` e **`PublishTrimmed=false`** (WPF trimado quebra em runtime). Um exe (~80MB), sem instalador, sem runtime. Autostart = presença na Run key `HKCU\...\Run` (sem estado duplicado no settings). Exe não assinado: aviso SmartScreen esperado no primeiro uso — comunicar aos amigos.

## 9. Testes (v1)

xUnit somente onde há lógica pura: `CredentialStore` (parse tolerante com campos desconhecidos, seleção arquivo×cache, expiração, comportamento pós-logout), mapeamento valor→zona de cor (limiares 70/90), regra posicional dos segmentos LED, truncamento de tooltip ≤127. UI, tray e Win32 ficam manuais.

## 10. Fora de escopo v1 (consciente)

Auto-hide em fullscreen · acrylic experimental · multi-conta · auto-update · assinatura de código · AppBar · notificações toast em limiar. Candidatos a v2 se a v1 provar valor.
