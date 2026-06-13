# Release Pipeline + Aviso de Atualização — Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Publicar o `ClaudeWatch.exe` como GitHub Release a cada tag `v*` e fazer o app avisar (sem baixar/trocar nada) quando houver versão nova, por três canais: menu do tray, balão do Windows e rodapé do widget.

**Architecture:** Uma unidade isolada de update — `UpdateChecker` (lógica pura de comparação SemVer, testável) + `GitHubReleaseClient` (consulta a API pública do GitHub) + `UpdateService` (agenda checagens e publica `UpdateStatus`). O composition root liga o status aos três canais de UI. Nada toca a lógica de usage/credenciais existente. O pipeline é um GitHub Actions em `windows-latest` disparado por tag.

**Tech Stack:** net10.0-windows · WPF + WinForms · System.Text.Json · xUnit · GitHub Actions (`softprops/action-gh-release`).

**Spec:** `docs/superpowers/specs/2026-06-12-release-pipeline-autoupdate-design.md`.

**Contexto:** já estamos na branch `feature/release-autoupdate`. Repo público `github.com/carlosdealmeida/claude-watch`. Convenções: nullable enable, file-scoped namespaces, commits frequentes. O `FakeHttpHandler` de testes já existe em `tests/ClaudeWatch.Tests/FakeHttpHandler.cs` (captura `LastRequest`, `LastBody`, `LastContentType`).

---

### Task 1: Versionamento — `<Version>` no csproj + `AppVersion`

**Files:**
- Modify: `src/ClaudeWatch/ClaudeWatch.csproj`
- Create: `src/ClaudeWatch/Infrastructure/AppVersion.cs`

- [ ] **Step 1: Adicionar `<Version>` ao csproj**

No `PropertyGroup` principal de `src/ClaudeWatch/ClaudeWatch.csproj`, logo após `<RootNamespace>ClaudeWatch</RootNamespace>`, inserir:

```xml
    <Version>0.1.0</Version>
```

- [ ] **Step 2: Criar `AppVersion`**

```csharp
// src/ClaudeWatch/Infrastructure/AppVersion.cs
using System.Reflection;

namespace ClaudeWatch.Infrastructure;

public static class AppVersion
{
    /// <summary>Versão do assembly em execução (vem de &lt;Version&gt; no csproj / -p:Version no publish).</summary>
    public static Version Current { get; } =
        Assembly.GetEntryAssembly()?.GetName().Version ?? new Version(0, 0, 0);
}
```

- [ ] **Step 3: Build**

Run: `dotnet build ClaudeWatch.slnx -v q -nologo`
Expected: `Compilação com êxito.` / `0 Erro(s)`

- [ ] **Step 4: Commit**

```bash
git add src/ClaudeWatch/ClaudeWatch.csproj src/ClaudeWatch/Infrastructure/AppVersion.cs
git commit -m "feat: versao explicita no csproj e helper AppVersion"
```

---

### Task 2: `UpdateChecker` (Core, lógica pura) — TDD

**Files:**
- Create: `src/ClaudeWatch/Core/UpdateChecker.cs`
- Test: `tests/ClaudeWatch.Tests/UpdateCheckerTests.cs`

- [ ] **Step 1: Escrever o teste que falha**

```csharp
// tests/ClaudeWatch.Tests/UpdateCheckerTests.cs
using ClaudeWatch.Core;
using Xunit;

public class UpdateCheckerTests
{
    private const string Url = "https://github.com/x/releases/tag/v0.2.0";

    [Fact]
    public void Versao_maior_indica_update()
    {
        var s = UpdateChecker.Check(new Version(0, 1, 0), "v0.2.0", Url);
        Assert.True(s.Available);
        Assert.Equal("0.2.0", s.LatestVersion);
        Assert.Equal(Url, s.Url);
    }

    [Theory]
    [InlineData("0.1.0")]   // igual
    [InlineData("v0.1.0")]  // igual com prefixo
    [InlineData("v0.0.9")]  // menor
    public void Versao_igual_ou_menor_nao_indica_update(string tag) =>
        Assert.False(UpdateChecker.Check(new Version(0, 1, 0), tag, Url).Available);

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("nightly")]   // não parseável
    public void Tag_invalida_ou_ausente_nao_indica_update(string? tag) =>
        Assert.False(UpdateChecker.Check(new Version(0, 1, 0), tag, Url).Available);

    [Fact]
    public void Url_ausente_nao_indica_update() =>
        Assert.False(UpdateChecker.Check(new Version(0, 1, 0), "v9.9.9", null).Available);

    [Fact]
    public void Tag_sem_patch_e_tolerada()
    {
        var s = UpdateChecker.Check(new Version(0, 1, 0), "v0.2", Url);
        Assert.True(s.Available);
    }
}
```

- [ ] **Step 2: Rodar e ver falhar**

Run: `dotnet test ClaudeWatch.slnx --filter UpdateCheckerTests -v q -nologo`
Expected: FAIL — `UpdateChecker`/`UpdateStatus` não existem.

- [ ] **Step 3: Implementar**

```csharp
// src/ClaudeWatch/Core/UpdateChecker.cs
namespace ClaudeWatch.Core;

public sealed record UpdateStatus(bool Available, string? LatestVersion, string? Url)
{
    public static readonly UpdateStatus None = new(false, null, null);
}

public static class UpdateChecker
{
    public static UpdateStatus Check(Version current, string? latestTag, string? url)
    {
        if (string.IsNullOrWhiteSpace(latestTag) || string.IsNullOrWhiteSpace(url))
            return UpdateStatus.None;

        var cleaned = latestTag.Trim().TrimStart('v', 'V');
        if (!Version.TryParse(cleaned, out var latest))
            return UpdateStatus.None;

        return Normalize(latest) > Normalize(current)
            ? new UpdateStatus(true, cleaned, url)
            : UpdateStatus.None;
    }

    // Ignora o componente Revision e trata Build ausente (-1) como 0,
    // para comparar "0.2" e "0.1.0.0" de forma consistente.
    private static Version Normalize(Version v) =>
        new(v.Major, v.Minor, v.Build < 0 ? 0 : v.Build);
}
```

- [ ] **Step 4: Rodar e ver passar**

Run: `dotnet test ClaudeWatch.slnx --filter UpdateCheckerTests -v q -nologo`
Expected: PASS (todos).

- [ ] **Step 5: Commit**

```bash
git add src/ClaudeWatch/Core/UpdateChecker.cs tests/ClaudeWatch.Tests/UpdateCheckerTests.cs
git commit -m "feat: UpdateChecker com comparacao SemVer tolerante"
```

---

### Task 3: `GitHubReleaseClient` (Infra) — TDD

**Files:**
- Create: `src/ClaudeWatch/Infrastructure/GitHubReleaseClient.cs`
- Test: `tests/ClaudeWatch.Tests/GitHubReleaseClientTests.cs`

- [ ] **Step 1: Escrever o teste que falha**

```csharp
// tests/ClaudeWatch.Tests/GitHubReleaseClientTests.cs
using System.Net;
using ClaudeWatch.Infrastructure;
using Xunit;

public class GitHubReleaseClientTests
{
    private const string Body =
        """{"tag_name":"v0.2.0","html_url":"https://github.com/x/claude-watch/releases/tag/v0.2.0","name":"0.2.0"}""";

    [Fact]
    public async Task Sucesso_extrai_tag_e_url()
    {
        var client = new GitHubReleaseClient(new HttpClient(new FakeHttpHandler(HttpStatusCode.OK, Body)));
        var r = await client.FetchLatestAsync(default);
        Assert.Equal("v0.2.0", r!.TagName);
        Assert.Equal("https://github.com/x/claude-watch/releases/tag/v0.2.0", r.HtmlUrl);
    }

    [Fact]
    public async Task Envia_user_agent()
    {
        var handler = new FakeHttpHandler(HttpStatusCode.OK, Body);
        await new GitHubReleaseClient(new HttpClient(handler)).FetchLatestAsync(default);
        Assert.Equal("ClaudeWatch", handler.LastRequest!.Headers.UserAgent.ToString());
    }

    [Theory]
    [InlineData(HttpStatusCode.NotFound)]   // repo sem releases
    [InlineData(HttpStatusCode.Forbidden)]  // rate-limit
    public async Task Erro_http_retorna_null(HttpStatusCode code)
    {
        var client = new GitHubReleaseClient(new HttpClient(new FakeHttpHandler(code, "{}")));
        Assert.Null(await client.FetchLatestAsync(default));
    }
}
```

- [ ] **Step 2: Rodar e ver falhar**

Run: `dotnet test ClaudeWatch.slnx --filter GitHubReleaseClientTests -v q -nologo`
Expected: FAIL — `GitHubReleaseClient`/`LatestRelease` não existem.

- [ ] **Step 3: Implementar**

```csharp
// src/ClaudeWatch/Infrastructure/GitHubReleaseClient.cs
using System.Text.Json.Nodes;

namespace ClaudeWatch.Infrastructure;

public sealed record LatestRelease(string TagName, string HtmlUrl);

public sealed class GitHubReleaseClient(HttpClient http)
{
    public const string Endpoint =
        "https://api.github.com/repos/carlosdealmeida/claude-watch/releases/latest";

    public async Task<LatestRelease?> FetchLatestAsync(CancellationToken ct)
    {
        try
        {
            using var req = new HttpRequestMessage(HttpMethod.Get, Endpoint);
            req.Headers.TryAddWithoutValidation("User-Agent", "ClaudeWatch"); // GitHub exige UA
            req.Headers.TryAddWithoutValidation("Accept", "application/vnd.github+json");
            using var resp = await http.SendAsync(req, ct);
            if (!resp.IsSuccessStatusCode) return null;

            var node = JsonNode.Parse(await resp.Content.ReadAsStringAsync(ct));
            var tag = node?["tag_name"]?.GetValue<string>();
            var url = node?["html_url"]?.GetValue<string>();
            return tag is not null && url is not null ? new LatestRelease(tag, url) : null;
        }
        catch { return null; } // rede fora / JSON inesperado: sem update, nunca quebra
    }
}
```

- [ ] **Step 4: Rodar e ver passar**

Run: `dotnet test ClaudeWatch.slnx --filter GitHubReleaseClientTests -v q -nologo`
Expected: PASS.

- [ ] **Step 5: Commit**

```bash
git add src/ClaudeWatch/Infrastructure/GitHubReleaseClient.cs tests/ClaudeWatch.Tests/GitHubReleaseClientTests.cs
git commit -m "feat: GitHubReleaseClient (releases/latest, tolerante a erro)"
```

---

### Task 4: `UpdateService` (orquestração) — TDD

**Files:**
- Create: `src/ClaudeWatch/Infrastructure/UpdateService.cs`
- Test: `tests/ClaudeWatch.Tests/UpdateServiceTests.cs`

- [ ] **Step 1: Escrever o teste que falha**

```csharp
// tests/ClaudeWatch.Tests/UpdateServiceTests.cs
using System.Net;
using ClaudeWatch.Core;
using ClaudeWatch.Infrastructure;
using Xunit;

public class UpdateServiceTests
{
    private static UpdateService Service(HttpStatusCode code, string body, Action<UpdateStatus> publish) =>
        new(new Version(0, 1, 0),
            new GitHubReleaseClient(new HttpClient(new FakeHttpHandler(code, body))),
            publish, _ => { });

    [Fact]
    public async Task Versao_nova_publica_disponivel()
    {
        UpdateStatus? pub = null;
        var svc = Service(HttpStatusCode.OK,
            """{"tag_name":"v9.9.9","html_url":"https://x/r"}""", s => pub = s);
        var status = await svc.CheckAsync(default);
        Assert.True(status.Available);
        Assert.Equal("9.9.9", status.LatestVersion);
        Assert.True(pub!.Available);
    }

    [Fact]
    public async Task Sem_release_publica_indisponivel()
    {
        UpdateStatus? pub = null;
        var svc = Service(HttpStatusCode.NotFound, "{}", s => pub = s);
        var status = await svc.CheckAsync(default);
        Assert.False(status.Available);
        Assert.False(pub!.Available);
    }
}
```

- [ ] **Step 2: Rodar e ver falhar**

Run: `dotnet test ClaudeWatch.slnx --filter UpdateServiceTests -v q -nologo`
Expected: FAIL — `UpdateService` não existe.

- [ ] **Step 3: Implementar**

```csharp
// src/ClaudeWatch/Infrastructure/UpdateService.cs
using ClaudeWatch.Core;

namespace ClaudeWatch.Infrastructure;

public sealed class UpdateService(
    Version currentVersion,
    GitHubReleaseClient client,
    Action<UpdateStatus> publish,
    Action<string> log)
{
    public async Task<UpdateStatus> CheckAsync(CancellationToken ct)
    {
        try
        {
            var latest = await client.FetchLatestAsync(ct);
            var status = UpdateChecker.Check(currentVersion, latest?.TagName, latest?.HtmlUrl);
            publish(status);
            return status;
        }
        catch (Exception ex)
        {
            log($"update check: {ex.GetType().Name}: {ex.Message}");
            return UpdateStatus.None;
        }
    }

    public async Task RunAsync(TimeSpan initialDelay, TimeSpan interval, CancellationToken ct)
    {
        try { await Task.Delay(initialDelay, ct); } catch (OperationCanceledException) { return; }
        while (!ct.IsCancellationRequested)
        {
            await CheckAsync(ct);
            try { await Task.Delay(interval, ct); } catch (OperationCanceledException) { break; }
        }
    }
}
```

- [ ] **Step 4: Rodar e ver passar (suíte inteira)**

Run: `dotnet test ClaudeWatch.slnx -v q -nologo`
Expected: PASS — 45 anteriores + 15 novos (UpdateChecker 9, GitHubReleaseClient 4, UpdateService 2) = 60.

- [ ] **Step 5: Commit**

```bash
git add src/ClaudeWatch/Infrastructure/UpdateService.cs tests/ClaudeWatch.Tests/UpdateServiceTests.cs
git commit -m "feat: UpdateService (checagem agendada, falha silenciosa)"
```

---

### Task 5: `TrayController` — item de update + balão

**Files:**
- Modify: `src/ClaudeWatch/Tray/TrayController.cs`

- [ ] **Step 1: Promover o menu a campo e guardar estado de update**

Em `TrayController`, trocar a declaração local `var menu = new ContextMenuStrip();` por um campo. No topo da classe, junto aos outros campos, adicionar:

```csharp
    private readonly ContextMenuStrip _menu;
    private ToolStripMenuItem? _updateItem;
    private string? _updateUrl;
    private string? _lastNotifiedVersion;
```

No construtor, substituir `var menu = new ContextMenuStrip();` por `_menu = new ContextMenuStrip();` e trocar todas as ocorrências de `menu.Items` por `_menu.Items` no construtor. Trocar `ContextMenuStrip = menu` por `ContextMenuStrip = _menu` na criação do `NotifyIcon`.

- [ ] **Step 2: Ligar o clique do balão (no construtor, após criar `_icon`)**

Logo após a linha `_icon.DoubleClick += (_, _) => ToggleWidget?.Invoke();`, adicionar:

```csharp
        _icon.BalloonTipClicked += (_, _) => OpenUpdateUrl();
```

- [ ] **Step 3: Adicionar os métodos de update (antes do `Dispose`)**

```csharp
    public void ShowUpdate(string version, string url)
    {
        _updateUrl = url;

        if (_updateItem is null)
        {
            _updateItem = new ToolStripMenuItem { ForeColor = System.Drawing.Color.FromArgb(0x4C, 0x9A, 0xFF) };
            _updateItem.Click += (_, _) => OpenUpdateUrl();
            _menu.Items.Insert(0, _updateItem);
            _menu.Items.Insert(1, new ToolStripSeparator());
        }
        _updateItem.Text = $"⬆ Baixar atualização ({version})";

        if (_lastNotifiedVersion != version) // balão só uma vez por versão
        {
            _lastNotifiedVersion = version;
            _icon.BalloonTipTitle = "ClaudeWatch";
            _icon.BalloonTipText = $"Versão {version} disponível — clique para baixar";
            _icon.ShowBalloonTip(5000);
        }
    }

    private void OpenUpdateUrl()
    {
        if (_updateUrl is null) return;
        try { System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(_updateUrl) { UseShellExecute = true }); }
        catch { /* sem navegador: ignora */ }
    }
```

- [ ] **Step 4: Build**

Run: `dotnet build ClaudeWatch.slnx -v q -nologo`
Expected: `Compilação com êxito.` / `0 Erro(s)`

- [ ] **Step 5: Commit**

```bash
git add src/ClaudeWatch/Tray/TrayController.cs
git commit -m "feat: tray mostra item de update e balao (uma vez por versao)"
```

---

### Task 6: Widget — rodapé de update (ViewModel + XAML + converter)

**Files:**
- Modify: `src/ClaudeWatch/Widget/WidgetViewModel.cs`
- Modify: `src/ClaudeWatch/Widget/Skins.xaml`
- Modify: `src/ClaudeWatch/Widget/WidgetWindow.xaml`
- Modify: `src/ClaudeWatch/Widget/WidgetWindow.xaml.cs`

- [ ] **Step 1: Propriedades de update no ViewModel**

Em `WidgetViewModel`, antes de `private void Raise(...)`, adicionar:

```csharp
    private bool _updateAvailable;
    private string? _updateLabel;
    private string? _updateUrl;

    public bool UpdateAvailable { get => _updateAvailable; set { _updateAvailable = value; Raise(nameof(UpdateAvailable)); } }
    public string? UpdateLabel { get => _updateLabel; set { _updateLabel = value; Raise(nameof(UpdateLabel)); } }
    public string? UpdateUrl { get => _updateUrl; set { _updateUrl = value; Raise(nameof(UpdateUrl)); } }
```

- [ ] **Step 2: Registrar o `BooleanToVisibilityConverter` no `Skins.xaml`**

Logo após `<w:LocalHhmmConverter x:Key="LocalHhmm"/>`, adicionar:

```xml
  <BooleanToVisibilityConverter x:Key="BoolToVis"/>
```

- [ ] **Step 3: Rodapé de update no `WidgetWindow.xaml`**

Dentro do `<StackPanel>` do corpo, logo após o `</TextBlock>` do rodapé "atualizado às" (e antes do `</StackPanel>` que fecha o painel), inserir:

```xml
        <TextBlock Text="{Binding UpdateLabel}" Foreground="#4C9AFF" FontSize="10.5"
                   Margin="0,4,0,0" HorizontalAlignment="Center" Cursor="Hand"
                   MouseLeftButtonUp="OnUpdateClick"
                   Visibility="{Binding UpdateAvailable, Converter={StaticResource BoolToVis}}"/>
```

- [ ] **Step 4: Handler no code-behind `WidgetWindow.xaml.cs`**

Após o método `OnConnectAccount`, adicionar:

```csharp
    private void OnUpdateClick(object sender, MouseButtonEventArgs e)
    {
        if (DataContext is WidgetViewModel { UpdateUrl: { } url })
            try { Process.Start(new ProcessStartInfo(url) { UseShellExecute = true }); }
            catch { /* sem navegador: ignora */ }
    }
```

- [ ] **Step 5: Build**

Run: `dotnet build ClaudeWatch.slnx -v q -nologo`
Expected: `Compilação com êxito.` / `0 Erro(s)`

- [ ] **Step 6: Commit**

```bash
git add src/ClaudeWatch/Widget/WidgetViewModel.cs src/ClaudeWatch/Widget/Skins.xaml src/ClaudeWatch/Widget/WidgetWindow.xaml src/ClaudeWatch/Widget/WidgetWindow.xaml.cs
git commit -m "feat: rodape de update clicavel no widget"
```

---

### Task 7: Composition root — ligar `UpdateService` aos três canais

**Files:**
- Modify: `src/ClaudeWatch/App.xaml.cs`

- [ ] **Step 1: Criar e rodar o `UpdateService`**

Em `App.OnStartup`, logo após a linha `_ = poller.RunAsync(TimeSpan.FromSeconds(Math.Max(15, settings.IntervalSeconds)), _cts.Token);`, adicionar:

```csharp
        var updateService = new UpdateService(
            AppVersion.Current,
            new GitHubReleaseClient(http),
            publish: s => Dispatcher.Invoke(() =>
            {
                if (!s.Available) return;
                _tray!.ShowUpdate(s.LatestVersion!, s.Url!);
                vm.UpdateAvailable = true;
                vm.UpdateLabel = $"⬆ v{s.LatestVersion} disponível";
                vm.UpdateUrl = s.Url;
            }),
            log: log.Log);
        _ = updateService.RunAsync(TimeSpan.FromSeconds(10), TimeSpan.FromHours(6), _cts.Token);
```

(O `http` reutilizado é o mesmo `HttpClient` já criado para o pipeline de credenciais/usage.)

- [ ] **Step 2: Build**

Run: `dotnet build ClaudeWatch.slnx -v q -nologo`
Expected: `Compilação com êxito.` / `0 Erro(s)`

- [ ] **Step 3: Verificação manual (showroom de update)**

Temporariamente, para forçar um update visível sem depender de uma release real, rodar com a versão atual sendo baixa: o `AppVersion.Current` é 0.1.0 e, se já existir uma release `v0.1.0` ou maior no GitHub, o aviso aparece. Caso ainda não haja release publicada, pular esta verificação e validá-la no smoke da Task 8 (após a primeira release existir).

Se houver release ≥ a versão local: `dotnet run --project src/ClaudeWatch` → em ~10s o item "⬆ Baixar atualização (vX)" aparece no menu do tray, o balão surge uma vez, e o rodapé azul aparece no widget; clicar em qualquer um abre a página da release.

- [ ] **Step 4: Commit**

```bash
git add src/ClaudeWatch/App.xaml.cs
git commit -m "feat: composition root liga UpdateService aos tres canais de aviso"
```

---

### Task 8: Pipeline `release.yml` + smoke da primeira release

**Files:**
- Create: `.github/workflows/release.yml`

- [ ] **Step 1: Criar o workflow**

```yaml
# .github/workflows/release.yml
name: release

on:
  push:
    tags: ['v*']

permissions:
  contents: write

jobs:
  build:
    runs-on: windows-latest
    steps:
      - uses: actions/checkout@v4

      - uses: actions/setup-dotnet@v4
        with:
          dotnet-version: '10.0.x'

      - name: Test
        run: dotnet test ClaudeWatch.slnx -c Release

      - name: Version from tag
        id: ver
        shell: bash
        run: echo "v=${GITHUB_REF_NAME#v}" >> "$GITHUB_OUTPUT"

      - name: Publish single-file exe
        run: >
          dotnet publish src/ClaudeWatch -c Release -r win-x64 --self-contained
          -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true
          -p:PublishReadyToRun=true -p:PublishTrimmed=false
          -p:Version=${{ steps.ver.outputs.v }} -o publish

      - name: Create GitHub Release
        uses: softprops/action-gh-release@v2
        with:
          files: publish/ClaudeWatch.exe
          generate_release_notes: true
```

- [ ] **Step 2: Commit**

```bash
git add .github/workflows/release.yml
git commit -m "ci: pipeline de release por tag (build, test, publish, gh release)"
```

- [ ] **Step 3: Integrar a branch e enviar ao GitHub**

Concluir a branch (merge em `master` via finishing-a-development-branch) e então:

```bash
git push origin master
```

- [ ] **Step 4: Disparar a primeira release e fazer o smoke**

```bash
git tag v0.2.0
git push origin v0.2.0
```

Verificar (via `gh run watch` ou aba Actions):
- O workflow `release` roda em `windows-latest`, os testes passam, o publish gera o exe.
- A release `v0.2.0` aparece em `https://github.com/carlosdealmeida/claude-watch/releases` com `ClaudeWatch.exe` anexado e notas geradas.

Se `setup-dotnet` não achar o SDK `10.0.x` (canal preview), ajustar para incluir `dotnet-quality: preview` no passo `setup-dotnet` e re-tagear (`v0.2.1`).

- [ ] **Step 5: Smoke do aviso de update (ponta a ponta)**

Com a release `v0.2.0` publicada e o app local ainda em `0.1.0` (ou baixar o exe `v0.2.0` e, num segundo momento, simular versão anterior): rodar o app → em ~10s os três canais avisam da `v0.2.0` e abrem a página da release. Confirmar no log (`%AppData%\ClaudeWatch\logs`) que não há exceções.

---

## Execution Handoff

(preenchido pela skill após salvar)
