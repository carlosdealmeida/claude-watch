# ClaudeWatch — Pipeline de release + aviso de atualização

**Data:** 2026-06-12 · **Status:** design aprovado · **Repo:** github.com/carlosdealmeida/claude-watch (público)

Fechar o ClaudeWatch com (1) um pipeline GitHub Actions que publica o `.exe` como
GitHub Release a cada tag, e (2) um verificador embutido que avisa o usuário quando há
versão nova — sem baixar nem trocar o binário automaticamente.

## 1. Decisões registradas

| Decisão | Escolha | Motivo |
|---|---|---|
| Gatilho do pipeline | Push de tag `v*` | Controle explícito de quando lançar; casa com as tags já em uso (v0.1.0) |
| Nível de auto-update | **Apenas notificar** + abrir página de download | Zero risco de corromper o exe em uso; o swap de single-file em execução é complexo e fica fora de escopo |
| Fonte da versão (release) | A própria tag → `-p:Version=` no publish | Tag, nome da release e versão embutida no exe sempre em sincronia |
| Descoberta de update | `GET /releases/latest` da API pública do GitHub | Repo público (sem auth); o endpoint já ignora pre-releases/rascunhos |
| Canais de aviso | Menu do tray + balão do Windows + rodapé no widget | Redundância suave; o usuário percebe por qualquer um dos três |
| Frequência de checagem | ~10s após abrir, depois a cada 6h | Não atrasa o startup; uso varia devagar, 6h é folgado para o rate-limit (60/h) |

## 2. Versionamento

- `<Version>0.1.0</Version>` no `ClaudeWatch.csproj` (default local).
- No pipeline, a tag `v0.2.0` vira `-p:Version=0.2.0` no `dotnet publish`.
- Em runtime o app lê a própria versão via `Assembly.GetEntryAssembly()!.GetName().Version`
  (ou `AssemblyInformationalVersion`), exposta por um helper `AppVersion.Current`.

## 3. Pipeline — `.github/workflows/release.yml`

- **Gatilho:** `on: push: tags: ['v*']`.
- **Runner:** `windows-latest` (WPF + single-file win-x64 exigem Windows).
- **Permissão:** `permissions: contents: write` (criar release).
- **Passos:**
  1. `actions/checkout@v4`
  2. `actions/setup-dotnet@v4` com `dotnet-version: 10.0.x` (ajustar canal/`quality` se 10.0 estiver em preview no runner).
  3. `dotnet test ClaudeWatch.slnx -c Release` — **falha aqui aborta a release**.
  4. Derivar a versão: `tag` sem o prefixo `v` (ex.: `v0.2.0` → `0.2.0`).
  5. `dotnet publish src/ClaudeWatch -c Release -r win-x64 --self-contained -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true -p:PublishReadyToRun=true -p:PublishTrimmed=false -p:Version=<versão> -o publish`.
  6. `softprops/action-gh-release@v2` (ou `gh release create`): cria a release da tag, corpo com changelog automático (`generate_release_notes: true`), anexa `publish/ClaudeWatch.exe`.
- **Fluxo do mantenedor:** `git tag v0.2.0 && git push origin master --tags` → release pronta em ~3-5 min.

## 4. Auto-update no app

Unidade isolada, sem tocar a lógica de usage/credenciais.

### 4.1 `UpdateChecker` (Core — lógica pura, testável)
- Entrada: versão atual (`Version`) + `tag_name` da última release.
- Normaliza o prefixo `v`, faz parse SemVer (`major.minor.patch`) e compara.
- Saída: `UpdateStatus(bool Available, string? LatestVersion, string? Url)`.
- Tolerante: tag malformada, versão atual ≥ última, ou parse falho ⇒ `Available = false`. Nunca lança.

### 4.2 `GitHubReleaseClient` (Infrastructure)
- `GET https://api.github.com/repos/carlosdealmeida/claude-watch/releases/latest`.
- Headers: `User-Agent: ClaudeWatch` (obrigatório no GitHub), `Accept: application/vnd.github+json`.
- Parseia `tag_name` e `html_url`. Qualquer status ≠ 2xx (404 sem releases, 403 rate-limit) ⇒ retorna `null`.
- Retorna um `LatestRelease(string TagName, string HtmlUrl)` ou `null`.

### 4.3 `UpdateService` (orquestração)
- `Task<UpdateStatus> CheckAsync(ct)`: chama o client, passa para o `UpdateChecker`, devolve o status. Engole exceções (log).
- Loop próprio: primeira checagem ~10s após o start, depois a cada 6h (`PeriodicTimer`/`Task.Delay`), respeitando o `CancellationToken` do app.
- Publica o `UpdateStatus` via callback para a UI (Dispatcher), análogo ao `UsagePoller`.

### 4.4 Integração (composition root)
- `App.xaml.cs` cria `UpdateService` com `AppVersion.Current` e um `HttpClient`.
- No callback: se `Available`, dispara os 3 canais (uma vez por versão para o balão).

## 5. UX — os três canais

1. **Menu do tray:** item no topo **"⬆ Baixar atualização (vX)"**, visível só quando há update; `Click` → `Process.Start(url)`. Implementado no `TrayController` com um método `ShowUpdate(versao, url)` que insere/remove o item.
2. **Balão do Windows:** `NotifyIcon.ShowBalloonTip` **uma vez por versão** detectada (guarda a última versão avisada para não repetir a cada 6h). Clique no balão → abre a `url`.
3. **Widget:** rodapé extra discreto **"⬆ vX disponível"** (azul `#4C9AFF`, cursor de mão), abaixo do "atualizado às", visível só quando há update; `MouseLeftButtonUp` → abre a `url`. Exposto via uma propriedade no `WidgetViewModel` (`UpdateBadge`/`UpdateUrl`).

## 6. Testes (xUnit, sem rede)

- `UpdateChecker`: `0.1.0`<`0.2.0` ⇒ disponível; igual e maior ⇒ indisponível; `v`-prefixo tolerado; `1.0`/lixo ⇒ indisponível; URL repassada.
- `GitHubReleaseClient` (com `FakeHttpHandler`): fixture de `/releases/latest` ⇒ extrai `tag_name`/`html_url`; 404 e 403 ⇒ `null`; envia `User-Agent`.
- `UpdateService`: status `Available` propagado; exceção do client ⇒ status indisponível (não lança).

## 7. Resiliência e fora de escopo

- **Resiliência:** toda falha de rede / rate-limit é silenciosa (log em `%AppData%\ClaudeWatch\logs`). O aviso nunca bloqueia o app nem o poller de usage.
- **Fora de escopo (consciente):** download automático, troca do binário em uso, updater/relauncher, assinatura de código, canal beta. Candidatos a uma fase futura se houver demanda.

## 8. Arquivos

- Criar: `.github/workflows/release.yml`, `src/ClaudeWatch/Core/UpdateChecker.cs`, `src/ClaudeWatch/Infrastructure/AppVersion.cs`, `src/ClaudeWatch/Infrastructure/GitHubReleaseClient.cs`, `src/ClaudeWatch/Infrastructure/UpdateService.cs`, testes correspondentes.
- Modificar: `ClaudeWatch.csproj` (`<Version>`), `App.xaml.cs` (composition root), `Tray/TrayController.cs` (item de update + balão), `Widget/WidgetViewModel.cs` e `Widget/WidgetWindow.xaml` (rodapé de update).
