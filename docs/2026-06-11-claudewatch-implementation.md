# ClaudeWatch Implementation Plan

> **For Claude:** REQUIRED SUB-SKILL: Use superpowers:executing-plans to implement this plan task-by-task.

**Goal:** Widget desktop Windows (WPF) sempre visível + ícone de tray mostrando o consumo da assinatura Claude (5h, semana, Opus), lendo credenciais do Claude Code oficial — distribuível como um único exe.

**Architecture:** Processo único WPF + WinForms NotifyIcon. Núcleo puro testável (`Core/`, `Credentials/`) isolado de UI e I/O por abstrações pequenas. `CredentialPipeline` lê `~/.claude/.credentials.json` (read-only, invariante), renova com refresh token estável e cacheia em DPAPI. `UsagePoller` publica `UsageSnapshot` imutável para janela (dual-skin: Anéis/LED) e tray.

**Tech Stack:** net10.0-windows · WPF + WinForms (`UseWPF`+`UseWindowsForms`) · System.Text.Json · System.Security.Cryptography.ProtectedData · xUnit.

**Design doc:** `docs/plans/2026-06-11-claudewatch-widget-design.md` (fonte das decisões; este plano não as rediscute).

---

## ⚠ Pré-requisitos bloqueantes — TODO(carlos)

Preencher a partir do parity do CORTEX **antes do smoke final (Task 21)**. As tasks TDD passam sem isso (usam fakes/fixtures):

1. `OAuthConstants.ClientId` e `OAuthConstants.TokenEndpoint` (+ content-type do grant: JSON ou form-urlencoded) — Task 9.
2. `OAuthConstants.UsageEndpoint` + shape real da resposta de usage (atualizar fixture do teste) — Task 10.
3. Nomes exatos dos campos do `.credentials.json` (`claudeAiOauth.accessToken/refreshToken/expiresAt` assumidos) — Task 6.
4. URL do botão "Conectar conta" (`AppUrls.ClaudeCode`) — Task 19.

**Contexto:** rodar em worktree dedicada. Convenções: nullable enable, file-scoped namespaces, commits frequentes.

---

### Task 0: Worktree e esqueleto do repositório

**Files:** Create: `docs/plans/` (copiar os dois .md), `.gitignore`

**Step 1:** Na worktree: `mkdir -p docs/plans src tests` e copiar o design doc + este plano para `docs/plans/`.
**Step 2:** `.gitignore` padrão .NET: `dotnet new gitignore`
**Step 3:** Commit: `git add -A && git commit -m "docs: design e plano do ClaudeWatch"`

---

### Task 1: Bootstrap da solução

**Files:**
- Create: `ClaudeWatch.sln`, `src/ClaudeWatch/ClaudeWatch.csproj`, `src/ClaudeWatch/App.xaml`, `src/ClaudeWatch/App.xaml.cs`, `src/ClaudeWatch/app.manifest`
- Create: `tests/ClaudeWatch.Tests/ClaudeWatch.Tests.csproj`

**Step 1: gerar projetos**

```bash
dotnet new sln -n ClaudeWatch
dotnet new wpf -n ClaudeWatch -o src/ClaudeWatch
dotnet new xunit -n ClaudeWatch.Tests -o tests/ClaudeWatch.Tests
dotnet sln add src/ClaudeWatch tests/ClaudeWatch.Tests
dotnet add tests/ClaudeWatch.Tests reference src/ClaudeWatch
dotnet add src/ClaudeWatch package System.Security.Cryptography.ProtectedData
```

**Step 2: substituir `src/ClaudeWatch/ClaudeWatch.csproj`**

```xml
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <OutputType>WinExe</OutputType>
    <TargetFramework>net10.0-windows</TargetFramework>
    <UseWPF>true</UseWPF>
    <UseWindowsForms>true</UseWindowsForms>
    <Nullable>enable</Nullable>
    <ImplicitUsings>enable</ImplicitUsings>
    <RootNamespace>ClaudeWatch</RootNamespace>
    <ApplicationManifest>app.manifest</ApplicationManifest>
  </PropertyGroup>
  <ItemGroup>
    <PackageReference Include="System.Security.Cryptography.ProtectedData" Version="*" />
  </ItemGroup>
</Project>
```

(No csproj de testes, trocar o TFM para `net10.0-windows`.)

**Step 3: `app.manifest` (DPI PerMonitorV2)**

```xml
<?xml version="1.0" encoding="utf-8"?>
<assembly manifestVersion="1.0" xmlns="urn:schemas-microsoft-com:asm.v1">
  <application xmlns="urn:schemas-microsoft-com:asm.v3">
    <windowsSettings>
      <dpiAwareness xmlns="http://schemas.microsoft.com/SMI/2016/WindowsSettings">PerMonitorV2</dpiAwareness>
    </windowsSettings>
  </application>
</assembly>
```

**Step 4: `App.xaml`** — remover `StartupUri`, adicionar `ShutdownMode="OnExplicitShutdown"`. `App.xaml.cs` fica com `OnStartup` vazio por enquanto (composition root chega na Task 16). Apagar `MainWindow.xaml` gerado.

**Step 5:** `dotnet build` → `Build succeeded`. Commit: `git commit -am "chore: bootstrap WPF + xUnit (net10.0-windows)"`

---

### Task 2: Zonas de cor e modelo de snapshot

**Files:**
- Create: `src/ClaudeWatch/Core/Zone.cs`, `src/ClaudeWatch/Core/UsageSnapshot.cs`
- Test: `tests/ClaudeWatch.Tests/ZoneRulesTests.cs`

**Step 1: teste que falha**

```csharp
using ClaudeWatch.Core;
using Xunit;

public class ZoneRulesTests
{
    [Theory]
    [InlineData(0, Zone.Verde)] [InlineData(69, Zone.Verde)]
    [InlineData(70, Zone.Ambar)] [InlineData(89, Zone.Ambar)]
    [InlineData(90, Zone.Vermelho)] [InlineData(100, Zone.Vermelho)]
    public void Regua_70_90(int pct, Zone esperada) => Assert.Equal(esperada, ZoneRules.From(pct));

    [Fact]
    public void Worst_e_o_maior_percentual()
    {
        var s = Snapshots.Of(42, 78, 96);
        Assert.Equal("Opus", s.Worst.Label);
    }
}

public static class Snapshots
{
    public static UsageSnapshot Of(int h5, int sem, int opus, SnapshotState st = SnapshotState.Ok) =>
        new(new Meter("Sessão 5h", h5, null), new Meter("Semana", sem, null),
            new Meter("Opus", opus, null), DateTimeOffset.UtcNow, st);
}
```

**Step 2:** `dotnet test` → FAIL (tipos não existem).

**Step 3: implementação mínima**

```csharp
// Core/Zone.cs
namespace ClaudeWatch.Core;

public enum Zone { Verde, Ambar, Vermelho }

public static class ZoneRules
{
    public static Zone From(int pct) => pct >= 90 ? Zone.Vermelho : pct >= 70 ? Zone.Ambar : Zone.Verde;
    public static int Clamp(int pct) => Math.Clamp(pct, 0, 100);
}

public static class ZoneColors
{
    public const string Verde = "#3FB950";
    public const string Ambar = "#E8A23D";
    public const string Vermelho = "#F85149";
    public static string Hex(Zone z) => z switch
    { Zone.Vermelho => Vermelho, Zone.Ambar => Ambar, _ => Verde };
}
```

```csharp
// Core/UsageSnapshot.cs
namespace ClaudeWatch.Core;

public enum SnapshotState { Ok, Stale, NoCredential }

public sealed record Meter(string Label, int Pct, DateTimeOffset? ResetAt)
{
    public Zone Zone => ZoneRules.From(Pct);
}

public sealed record UsageSnapshot(
    Meter FiveHour, Meter Week, Meter Opus,
    DateTimeOffset CollectedAt, SnapshotState State)
{
    public Meter Worst => new[] { FiveHour, Week, Opus }.MaxBy(m => m.Pct)!;
}
```

**Step 4:** `dotnet test` → PASS. **Step 5:** `git commit -am "feat: régua de zonas e UsageSnapshot"`

---

### Task 3: Regra posicional dos LEDs (skin E)

**Files:** Create: `src/ClaudeWatch/Core/LedScale.cs` · Test: `tests/ClaudeWatch.Tests/LedScaleTests.cs`

**Step 1: teste que falha**

```csharp
using ClaudeWatch.Core;
using Xunit;

public class LedScaleTests
{
    [Theory] [InlineData(0,0)] [InlineData(42,6)] [InlineData(96,14)] [InlineData(100,15)]
    public void Quantidade_acesa(int pct, int acesos) =>
        Assert.Equal(acesos, LedScale.Build(pct).Count(s => s.Lit));

    [Fact]
    public void Cor_e_por_posicao_nao_por_valor()
    {
        var s = LedScale.Build(100); // tudo aceso: ponta verde, meio âmbar, topo vermelho
        Assert.Equal(Zone.Verde, s[0].Zone);
        Assert.Equal(Zone.Ambar, s[10].Zone);    // centro 70.0
        Assert.Equal(Zone.Vermelho, s[13].Zone); // centro 90.0
    }
}
```

**Step 2:** `dotnet test --filter LedScaleTests` → FAIL.

**Step 3: implementação**

```csharp
namespace ClaudeWatch.Core;

public sealed record LedSegment(bool Lit, Zone Zone);

public static class LedScale
{
    public const int Count = 15;

    public static IReadOnlyList<LedSegment> Build(int pct)
    {
        var p = ZoneRules.Clamp(pct);
        var segs = new LedSegment[Count];
        for (var i = 0; i < Count; i++)
        {
            var center = (i + 0.5) * 100.0 / Count;
            segs[i] = new LedSegment(center <= p, ZoneRules.From((int)Math.Round(center)));
        }
        return segs;
    }
}
```

**Step 4:** PASS. **Step 5:** `git commit -am "feat: escala LED com cor posicional"`

---

### Task 4: Formatador de tooltip (≤127 chars)

**Files:** Create: `src/ClaudeWatch/Core/TooltipFormatter.cs` · Test: `tests/ClaudeWatch.Tests/TooltipFormatterTests.cs`

**Step 1: teste que falha**

```csharp
using ClaudeWatch.Core;
using Xunit;

public class TooltipFormatterTests
{
    [Fact]
    public void Formato_showroom()
    {
        var s = Snapshots.Of(42, 78, 96) with
        { FiveHour = new Meter("Sessão 5h", 42, new DateTimeOffset(2026, 6, 11, 15, 0, 0, TimeSpan.Zero)) };
        var t = TooltipFormatter.Format(s, local: false);
        Assert.Equal("5h 42% (reset 15:00) · Sem 78% · Opus 96%", t);
        Assert.True(t.Length <= 127);
    }

    [Fact]
    public void Sem_reset_omite_parenteses() =>
        Assert.Equal("5h 10% · Sem 20% · Opus 30%", TooltipFormatter.Format(Snapshots.Of(10, 20, 30), local: false));
}
```

**Step 2:** FAIL. **Step 3:**

```csharp
namespace ClaudeWatch.Core;

public static class TooltipFormatter
{
    public static string Format(UsageSnapshot s, bool local = true)
    {
        var reset = s.FiveHour.ResetAt is { } r
            ? $" (reset {(local ? r.ToLocalTime() : r):HH:mm})" : "";
        var text = $"5h {s.FiveHour.Pct}%{reset} · Sem {s.Week.Pct}% · Opus {s.Opus.Pct}%";
        return text.Length <= 127 ? text : text[..127];
    }
}
```

**Step 4:** PASS. **Step 5:** `git commit -am "feat: tooltip do tray"`

---

### Task 5: Settings em %AppData%

**Files:** Create: `src/ClaudeWatch/Infrastructure/Settings.cs`, `src/ClaudeWatch/Infrastructure/AppPaths.cs` · Test: `tests/ClaudeWatch.Tests/SettingsStoreTests.cs`

**Step 1: teste que falha** (usar `Path.Combine(Path.GetTempPath(), Guid...)` como baseDir)

```csharp
using ClaudeWatch.Infrastructure;
using Xunit;

public class SettingsStoreTests
{
    private static string TempDir() => Directory.CreateDirectory(
        Path.Combine(Path.GetTempPath(), "cw-" + Guid.NewGuid())).FullName;

    [Fact]
    public void Ausente_retorna_defaults()
    {
        var s = new SettingsStore(TempDir()).Load();
        Assert.Equal("Aneis", s.Skin);
        Assert.Equal(60, s.IntervalSeconds);
        Assert.False(s.Locked);
    }

    [Fact]
    public void Round_trip()
    {
        var dir = TempDir(); var store = new SettingsStore(dir);
        store.Save(new Settings { PosX = 10, PosY = 20, Skin = "Led", Locked = true });
        var s = store.Load();
        Assert.Equal((10d, 20d, "Led", true), (s.PosX!.Value, s.PosY!.Value, s.Skin, s.Locked));
    }

    [Fact]
    public void Corrompido_degrada_para_defaults()
    {
        var dir = TempDir();
        File.WriteAllText(Path.Combine(dir, "settings.json"), "{nope");
        Assert.Equal("Aneis", new SettingsStore(dir).Load().Skin);
    }
}
```

**Step 2:** FAIL. **Step 3:**

```csharp
// Infrastructure/AppPaths.cs
namespace ClaudeWatch.Infrastructure;

public static class AppPaths
{
    public static string BaseDir { get; } = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "ClaudeWatch");
    public static string LogsDir { get; } = Path.Combine(BaseDir, "logs");
}
```

```csharp
// Infrastructure/Settings.cs
using System.Text.Json;

namespace ClaudeWatch.Infrastructure;

public sealed record Settings
{
    public double? PosX { get; init; }
    public double? PosY { get; init; }
    public string Skin { get; init; } = "Aneis"; // "Aneis" | "Led"
    public bool Locked { get; init; }
    public int IntervalSeconds { get; init; } = 60;
    public string? WslCredentialsPath { get; init; }
}

public sealed class SettingsStore(string baseDir)
{
    private readonly string _path = Path.Combine(baseDir, "settings.json");
    private static readonly JsonSerializerOptions Opt = new() { WriteIndented = true };

    public Settings Load()
    {
        try
        {
            return File.Exists(_path)
                ? JsonSerializer.Deserialize<Settings>(File.ReadAllText(_path), Opt) ?? new Settings()
                : new Settings();
        }
        catch { return new Settings(); }
    }

    public void Save(Settings s)
    {
        Directory.CreateDirectory(baseDir);
        File.WriteAllText(_path, JsonSerializer.Serialize(s, Opt));
    }
}
```

**Step 4:** PASS. **Step 5:** `git commit -am "feat: settings persistidos com load tolerante"`

---

### Task 6: Parse tolerante do .credentials.json

**Files:** Create: `src/ClaudeWatch/Credentials/OAuthCredential.cs`, `src/ClaudeWatch/Credentials/ClaudeCodeCredentialsParser.cs` · Test: `tests/ClaudeWatch.Tests/CredentialsParserTests.cs`

**Step 1: teste que falha**

```csharp
using ClaudeWatch.Credentials;
using Xunit;

public class CredentialsParserTests
{
    private const string Fixture = """
    {"claudeAiOauth":{"accessToken":"AT","refreshToken":"RT","expiresAt":1780000000000,
     "scopes":["x"],"campoDesconhecido":{"a":1}},"outroBloco":true}
    """;

    [Fact]
    public void Parse_ignora_campos_desconhecidos()
    {
        var c = ClaudeCodeCredentialsParser.TryParse(Fixture)!;
        Assert.Equal(("AT", "RT"), (c.AccessToken, c.RefreshToken));
        Assert.Equal(DateTimeOffset.FromUnixTimeMilliseconds(1780000000000), c.ExpiresAt);
    }

    [Theory] [InlineData("{nope")] [InlineData("{}")] [InlineData("""{"claudeAiOauth":{"refreshToken":"RT"}}""")]
    public void Invalido_retorna_null(string json) =>
        Assert.Null(ClaudeCodeCredentialsParser.TryParse(json));
}
```

**Step 2:** FAIL. **Step 3:**

```csharp
// Credentials/OAuthCredential.cs
namespace ClaudeWatch.Credentials;

public sealed record OAuthCredential(string AccessToken, string? RefreshToken, DateTimeOffset ExpiresAt);
```

```csharp
// Credentials/ClaudeCodeCredentialsParser.cs
using System.Text.Json.Nodes;

namespace ClaudeWatch.Credentials;

public static class ClaudeCodeCredentialsParser
{
    // TODO(carlos): confirmar nomes de campos contra o parity do CORTEX.
    public static OAuthCredential? TryParse(string json)
    {
        try
        {
            var root = JsonNode.Parse(json);
            var o = root?["claudeAiOauth"] ?? root;
            var access = o?["accessToken"]?.GetValue<string>();
            if (string.IsNullOrEmpty(access)) return null;
            var refresh = o?["refreshToken"]?.GetValue<string>();
            var expMs = o?["expiresAt"]?.GetValue<long>() ?? 0;
            return new OAuthCredential(access, refresh, DateTimeOffset.FromUnixTimeMilliseconds(expMs));
        }
        catch { return null; }
    }
}
```

**Step 4:** PASS. **Step 5:** `git commit -am "feat: parse tolerante das credenciais do Claude Code"`

---

### Task 7: Seleção de token (arquivo × cache)

**Files:** Create: `src/ClaudeWatch/Credentials/TokenSelector.cs` · Test: `tests/ClaudeWatch.Tests/TokenSelectorTests.cs`

**Step 1: teste que falha**

```csharp
using ClaudeWatch.Credentials;
using Xunit;

public class TokenSelectorTests
{
    private static readonly DateTimeOffset Now = DateTimeOffset.UnixEpoch.AddDays(1000);
    private static readonly TimeSpan Margin = TimeSpan.FromMinutes(2);
    private static OAuthCredential C(string t, int mins) => new(t, null, Now.AddMinutes(mins));

    [Fact] public void Escolhe_o_mais_fresco() =>
        Assert.Equal("cache", TokenSelector.PickValid(C("file", 10), C("cache", 60), Now, Margin)!.AccessToken);

    [Fact] public void Margem_invalida_token_quase_expirado() =>
        Assert.Null(TokenSelector.PickValid(C("file", 1), null, Now, Margin));

    [Fact] public void Ambos_expirados_retorna_null() =>
        Assert.Null(TokenSelector.PickValid(C("a", -5), C("b", -1), Now, Margin));
}
```

**Step 2:** FAIL. **Step 3:**

```csharp
namespace ClaudeWatch.Credentials;

public static class TokenSelector
{
    public static OAuthCredential? PickValid(
        OAuthCredential? file, OAuthCredential? cache, DateTimeOffset now, TimeSpan margin) =>
        new[] { file, cache }
            .Where(c => c is not null && c.ExpiresAt > now + margin)
            .OrderByDescending(c => c!.ExpiresAt)
            .FirstOrDefault();
}
```

**Step 4:** PASS. **Step 5:** `git commit -am "feat: seleção do access token mais fresco com margem proativa"`

---

### Task 8: Cache DPAPI do token renovado

**Files:** Create: `src/ClaudeWatch/Credentials/TokenCache.cs` · Test: `tests/ClaudeWatch.Tests/TokenCacheTests.cs`

**Step 1: teste que falha** (roda em Windows; DPAPI CurrentUser)

```csharp
using ClaudeWatch.Credentials;
using Xunit;

public class TokenCacheTests
{
    private static string TempDir() => Directory.CreateDirectory(
        Path.Combine(Path.GetTempPath(), "cw-" + Guid.NewGuid())).FullName;

    [Fact]
    public void Round_trip()
    {
        var cache = new TokenCache(TempDir());
        cache.Save(new OAuthCredential("AT", null, DateTimeOffset.UnixEpoch.AddDays(1)));
        var c = cache.Load()!;
        Assert.Equal("AT", c.AccessToken);
    }

    [Fact]
    public void Bytes_adulterados_retornam_null()
    {
        var dir = TempDir(); var cache = new TokenCache(dir);
        cache.Save(new OAuthCredential("AT", null, DateTimeOffset.UnixEpoch));
        File.WriteAllBytes(Path.Combine(dir, "token.bin"), [1, 2, 3]);
        Assert.Null(cache.Load());
    }

    [Fact] public void Clear_e_ausente_sao_null()
    { var c = new TokenCache(TempDir()); c.Clear(); Assert.Null(c.Load()); }
}
```

**Step 2:** FAIL. **Step 3:**

```csharp
using System.Security.Cryptography;
using System.Text.Json;

namespace ClaudeWatch.Credentials;

public sealed class TokenCache(string baseDir)
{
    private readonly string _path = Path.Combine(baseDir, "token.bin");
    private sealed record CachedToken(string AccessToken, DateTimeOffset ExpiresAt);

    public void Save(OAuthCredential cred)
    {
        Directory.CreateDirectory(baseDir);
        var payload = JsonSerializer.SerializeToUtf8Bytes(new CachedToken(cred.AccessToken, cred.ExpiresAt));
        File.WriteAllBytes(_path, ProtectedData.Protect(payload, null, DataProtectionScope.CurrentUser));
    }

    public OAuthCredential? Load()
    {
        try
        {
            if (!File.Exists(_path)) return null;
            var raw = ProtectedData.Unprotect(File.ReadAllBytes(_path), null, DataProtectionScope.CurrentUser);
            var t = JsonSerializer.Deserialize<CachedToken>(raw);
            return t is null ? null : new OAuthCredential(t.AccessToken, null, t.ExpiresAt);
        }
        catch { return null; }
    }

    public void Clear() { try { File.Delete(_path); } catch { /* best effort */ } }
}
```

**Step 4:** PASS. **Step 5:** `git commit -am "feat: cache DPAPI do access token renovado"`

---

### Task 9: Cliente de refresh OAuth

**Files:** Create: `src/ClaudeWatch/Credentials/OAuthConstants.cs`, `src/ClaudeWatch/Credentials/OAuthRefreshClient.cs` · Test: `tests/ClaudeWatch.Tests/OAuthRefreshClientTests.cs` (+ `FakeHttpHandler.cs`)

**Step 1: teste que falha**

```csharp
using System.Net;
using ClaudeWatch.Credentials;
using Xunit;

public sealed class FakeHttpHandler(HttpStatusCode code, string body) : HttpMessageHandler
{
    public HttpRequestMessage? LastRequest;
    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage r, CancellationToken ct)
    { LastRequest = r; return Task.FromResult(new HttpResponseMessage(code) { Content = new StringContent(body) }); }
}

public class OAuthRefreshClientTests
{
    private static OAuthRefreshClient Client(HttpStatusCode code, string body) =>
        new(new HttpClient(new FakeHttpHandler(code, body)));

    [Fact]
    public async Task Sucesso_retorna_credencial_com_expiracao()
    {
        var r = await Client(HttpStatusCode.OK,
            """{"access_token":"NEW","expires_in":3600,"refresh_token":"RT"}""").RefreshAsync("RT", default);
        Assert.Equal("NEW", r.Credential!.AccessToken);
        Assert.False(r.Rejected);
        Assert.False(r.RotationDetected);
    }

    [Fact]
    public async Task Refresh_token_diferente_sinaliza_rotacao()
    {
        var r = await Client(HttpStatusCode.OK,
            """{"access_token":"NEW","expires_in":60,"refresh_token":"OUTRO"}""").RefreshAsync("RT", default);
        Assert.True(r.RotationDetected);
    }

    [Fact]
    public async Task Quatrocentos_e_rejeicao()
    {
        var r = await Client(HttpStatusCode.BadRequest, "{}").RefreshAsync("RT", default);
        Assert.True(r.Rejected);
        Assert.Null(r.Credential);
    }
}
```

**Step 2:** FAIL. **Step 3:**

```csharp
// Credentials/OAuthConstants.cs
namespace ClaudeWatch.Credentials;

public static class OAuthConstants
{
    // TODO(carlos): copiar do parity do CORTEX (bloqueia apenas o runtime real, não os testes).
    public const string ClientId = "TODO-client-id";
    public const string TokenEndpoint = "https://TODO/token";
    public const string UsageEndpoint = "https://TODO/usage";
}
```

```csharp
// Credentials/OAuthRefreshClient.cs
using System.Net.Http.Json;
using System.Text.Json.Nodes;

namespace ClaudeWatch.Credentials;

public sealed record RefreshResult(OAuthCredential? Credential, bool Rejected, bool RotationDetected);

public sealed class OAuthRefreshClient(HttpClient http)
{
    public async Task<RefreshResult> RefreshAsync(string refreshToken, CancellationToken ct)
    {
        // TODO(carlos): confirmar se o grant é JSON ou form-urlencoded no parity.
        using var resp = await http.PostAsJsonAsync(OAuthConstants.TokenEndpoint,
            new { grant_type = "refresh_token", refresh_token = refreshToken, client_id = OAuthConstants.ClientId }, ct);

        if ((int)resp.StatusCode is >= 400 and < 500) return new(null, Rejected: true, false);
        resp.EnsureSuccessStatusCode();

        var node = JsonNode.Parse(await resp.Content.ReadAsStringAsync(ct))!;
        var access = node["access_token"]!.GetValue<string>();
        var expiresIn = node["expires_in"]?.GetValue<int>() ?? 3600;
        var newRefresh = node["refresh_token"]?.GetValue<string>();

        var cred = new OAuthCredential(access, refreshToken, DateTimeOffset.UtcNow.AddSeconds(expiresIn));
        return new(cred, false, RotationDetected: newRefresh is not null && newRefresh != refreshToken);
    }
}
```

**Step 4:** PASS. **Step 5:** `git commit -am "feat: refresh OAuth com detecção de rotação"`

---

### Task 10: Cliente e parser de usage

**Files:** Create: `src/ClaudeWatch/Credentials/UsageClient.cs`, `src/ClaudeWatch/Core/UsageResponseParser.cs` · Test: `tests/ClaudeWatch.Tests/UsageTests.cs`

**Step 1: teste que falha**

```csharp
using System.Net;
using ClaudeWatch.Core;
using ClaudeWatch.Credentials;
using Xunit;

public class UsageTests
{
    // TODO(carlos): substituir pelo shape real do parity e ajustar o parser.
    private const string Fixture = """
    {"five_hour":{"utilization":42.4,"resets_at":"2026-06-11T15:00:00Z"},
     "seven_day":{"utilization":78.0,"resets_at":"2026-06-18T09:00:00Z"},
     "seven_day_opus":{"utilization":96.0,"resets_at":"2026-06-18T09:00:00Z"}}
    """;

    [Fact]
    public void Parser_mapeia_os_tres_medidores()
    {
        var s = UsageResponseParser.Parse(Fixture, DateTimeOffset.UnixEpoch);
        Assert.Equal((42, 78, 96), (s.FiveHour.Pct, s.Week.Pct, s.Opus.Pct));
        Assert.Equal(SnapshotState.Ok, s.State);
        Assert.NotNull(s.FiveHour.ResetAt);
    }

    [Fact]
    public async Task Quatrocentos_e_um_vira_UnauthorizedException()
    {
        var client = new UsageClient(new HttpClient(new FakeHttpHandler(HttpStatusCode.Unauthorized, "")));
        await Assert.ThrowsAsync<UnauthorizedException>(() => client.FetchAsync("AT", default));
    }

    [Fact]
    public async Task Sucesso_envia_bearer_e_parseia()
    {
        var handler = new FakeHttpHandler(HttpStatusCode.OK, Fixture);
        var s = await new UsageClient(new HttpClient(handler)).FetchAsync("AT", default);
        Assert.Equal("Bearer", handler.LastRequest!.Headers.Authorization!.Scheme);
        Assert.Equal(96, s.Opus.Pct);
    }
}
```

**Step 2:** FAIL. **Step 3:**

```csharp
// Core/UsageResponseParser.cs
using System.Text.Json.Nodes;

namespace ClaudeWatch.Core;

public static class UsageResponseParser
{
    // TODO(carlos): ajustar chaves ao shape real (parity do CORTEX).
    public static UsageSnapshot Parse(string json, DateTimeOffset now)
    {
        var n = JsonNode.Parse(json)!;
        Meter M(string key, string label) => new(
            label,
            ZoneRules.Clamp((int)Math.Round(n[key]!["utilization"]!.GetValue<double>())),
            n[key]!["resets_at"] is { } r ? DateTimeOffset.Parse(r.GetValue<string>()) : null);
        return new UsageSnapshot(M("five_hour", "Sessão 5h"), M("seven_day", "Semana"),
            M("seven_day_opus", "Opus"), now, SnapshotState.Ok);
    }
}
```

```csharp
// Credentials/UsageClient.cs
using System.Net;
using ClaudeWatch.Core;

namespace ClaudeWatch.Credentials;

public sealed class UnauthorizedException : Exception;

public sealed class UsageClient(HttpClient http)
{
    public async Task<UsageSnapshot> FetchAsync(string accessToken, CancellationToken ct)
    {
        using var req = new HttpRequestMessage(HttpMethod.Get, OAuthConstants.UsageEndpoint);
        req.Headers.Authorization = new("Bearer", accessToken);
        using var resp = await http.SendAsync(req, ct);
        if (resp.StatusCode == HttpStatusCode.Unauthorized) throw new UnauthorizedException();
        resp.EnsureSuccessStatusCode();
        return UsageResponseParser.Parse(await resp.Content.ReadAsStringAsync(ct), DateTimeOffset.UtcNow);
    }
}
```

**Step 4:** PASS. **Step 5:** `git commit -am "feat: cliente de usage com parser isolado"`

---

### Task 11: Política de backoff

**Files:** Create: `src/ClaudeWatch/Core/BackoffPolicy.cs` · Test: `tests/ClaudeWatch.Tests/BackoffPolicyTests.cs`

**Step 1: teste que falha**

```csharp
using ClaudeWatch.Core;
using Xunit;

public class BackoffPolicyTests
{
    [Fact]
    public void Sequencia_1_2_5_com_teto_e_reset()
    {
        var normal = TimeSpan.FromSeconds(60);
        var b = new BackoffPolicy();
        Assert.Equal(normal, b.NextDelay(normal));
        b.Failure(); Assert.Equal(TimeSpan.FromMinutes(1), b.NextDelay(normal));
        b.Failure(); Assert.Equal(TimeSpan.FromMinutes(2), b.NextDelay(normal));
        b.Failure(); Assert.Equal(TimeSpan.FromMinutes(5), b.NextDelay(normal));
        b.Failure(); Assert.Equal(TimeSpan.FromMinutes(5), b.NextDelay(normal));
        b.Success(); Assert.Equal(normal, b.NextDelay(normal));
    }
}
```

**Step 2:** FAIL. **Step 3:**

```csharp
namespace ClaudeWatch.Core;

public sealed class BackoffPolicy
{
    private static readonly TimeSpan[] Steps =
        [TimeSpan.FromMinutes(1), TimeSpan.FromMinutes(2), TimeSpan.FromMinutes(5)];
    private int _failures;

    public void Failure() => _failures++;
    public void Success() => _failures = 0;
    public TimeSpan NextDelay(TimeSpan normal) =>
        _failures == 0 ? normal : Steps[Math.Min(_failures - 1, Steps.Length - 1)];
}
```

**Step 4:** PASS. **Step 5:** `git commit -am "feat: backoff 1-2-5 com reset"`

---

### Task 12: CredentialPipeline (orquestração)

**Files:** Create: `src/ClaudeWatch/Credentials/ICredentialFile.cs`, `src/ClaudeWatch/Credentials/CredentialPipeline.cs`, `src/ClaudeWatch/Credentials/CredentialPaths.cs`, `src/ClaudeWatch/Credentials/FileCredentialFile.cs` · Test: `tests/ClaudeWatch.Tests/CredentialPipelineTests.cs`, `tests/ClaudeWatch.Tests/CredentialPathsTests.cs`

**Step 1: testes que falham**

```csharp
using ClaudeWatch.Credentials;
using ClaudeWatch.Infrastructure;
using Xunit;

public sealed class FakeCredFile(string? json) : ICredentialFile
{
    public string? Json = json;
    public string? ReadOrNull() => Json;
    public event Action? Changed { add { } remove { } }
}

public class CredentialPipelineTests
{
    private static readonly DateTimeOffset Now = DateTimeOffset.UnixEpoch.AddDays(1000);
    private static string CredJson(long expMs, string rt = "RT") =>
        $$"""{"claudeAiOauth":{"accessToken":"FILE","refreshToken":"{{rt}}","expiresAt":{{expMs}}}}""";
    private static string TempDir() => Directory.CreateDirectory(
        Path.Combine(Path.GetTempPath(), "cw-" + Guid.NewGuid())).FullName;

    private static CredentialPipeline Pipe(ICredentialFile f, TokenCache cache,
        System.Net.HttpStatusCode code, string body) =>
        new(f, cache, new OAuthRefreshClient(new HttpClient(new FakeHttpHandler(code, body))), _ => { });

    [Fact]
    public async Task Arquivo_valido_nao_refresca()
    {
        var p = Pipe(new FakeCredFile(CredJson(Now.AddHours(1).ToUnixTimeMilliseconds())),
            new TokenCache(TempDir()), System.Net.HttpStatusCode.InternalServerError, "");
        Assert.Equal("FILE", await p.GetAccessTokenAsync(Now, default));
        Assert.False(p.NoCredential);
    }

    [Fact]
    public async Task Expirado_refresca_e_cacheia()
    {
        var cache = new TokenCache(TempDir());
        var p = Pipe(new FakeCredFile(CredJson(Now.AddMinutes(-1).ToUnixTimeMilliseconds())), cache,
            System.Net.HttpStatusCode.OK, """{"access_token":"NEW","expires_in":3600}""");
        Assert.Equal("NEW", await p.GetAccessTokenAsync(Now, default));
        Assert.Equal("NEW", cache.Load()!.AccessToken);
    }

    [Fact]
    public async Task Refresh_rejeitado_limpa_cache_e_marca_NoCredential()
    {
        var cache = new TokenCache(TempDir());
        cache.Save(new OAuthCredential("OLD", null, Now.AddMinutes(-5)));
        var p = Pipe(new FakeCredFile(CredJson(Now.AddMinutes(-1).ToUnixTimeMilliseconds())), cache,
            System.Net.HttpStatusCode.BadRequest, "{}");
        Assert.Null(await p.GetAccessTokenAsync(Now, default));
        Assert.True(p.NoCredential);
        Assert.Null(cache.Load());
    }

    [Fact]
    public async Task Sem_arquivo_e_NoCredential()
    {
        var p = Pipe(new FakeCredFile(null), new TokenCache(TempDir()),
            System.Net.HttpStatusCode.OK, "{}");
        Assert.Null(await p.GetAccessTokenAsync(Now, default));
        Assert.True(p.NoCredential);
    }
}

public class CredentialPathsTests
{
    [Fact]
    public void Primario_existente_vence_fallback_wsl()
    {
        var home = Directory.CreateDirectory(Path.Combine(Path.GetTempPath(), "h-" + Guid.NewGuid())).FullName;
        var primary = Path.Combine(home, ".claude", ".credentials.json");
        Directory.CreateDirectory(Path.GetDirectoryName(primary)!);
        File.WriteAllText(primary, "{}");
        Assert.Equal(primary, CredentialPaths.Resolve(home, new Settings { WslCredentialsPath = @"\\wsl$\x" }));
    }

    [Fact]
    public void Sem_primario_usa_wsl_configurado()
    {
        var home = Directory.CreateDirectory(Path.Combine(Path.GetTempPath(), "h-" + Guid.NewGuid())).FullName;
        Assert.Equal(@"\\wsl$\x", CredentialPaths.Resolve(home, new Settings { WslCredentialsPath = @"\\wsl$\x" }));
    }
}
```

**Step 2:** FAIL. **Step 3:**

```csharp
// Credentials/ICredentialFile.cs
namespace ClaudeWatch.Credentials;

public interface ICredentialFile
{
    string? ReadOrNull();
    event Action? Changed;
}
```

```csharp
// Credentials/CredentialPaths.cs
using ClaudeWatch.Infrastructure;

namespace ClaudeWatch.Credentials;

public static class CredentialPaths
{
    public static string Resolve(string homeDir, Settings settings)
    {
        var primary = Path.Combine(homeDir, ".claude", ".credentials.json");
        if (File.Exists(primary)) return primary;
        return settings.WslCredentialsPath ?? primary;
    }
}
```

```csharp
// Credentials/FileCredentialFile.cs
namespace ClaudeWatch.Credentials;

public sealed class FileCredentialFile : ICredentialFile, IDisposable
{
    private readonly string _path;
    private readonly FileSystemWatcher? _watcher;
    public event Action? Changed;

    public FileCredentialFile(string path)
    {
        _path = path;
        var dir = Path.GetDirectoryName(path);
        if (dir is not null && Directory.Exists(dir))
        {
            _watcher = new FileSystemWatcher(dir, Path.GetFileName(path))
            { EnableRaisingEvents = true, NotifyFilter = NotifyFilters.LastWrite | NotifyFilters.FileName };
            _watcher.Changed += (_, _) => Changed?.Invoke();
            _watcher.Created += (_, _) => Changed?.Invoke();
            _watcher.Renamed += (_, _) => Changed?.Invoke();
        }
    }

    public string? ReadOrNull()
    {
        try { return File.Exists(_path) ? File.ReadAllText(_path) : null; }
        catch { return null; } // arquivo pode estar em escrita pelo CC; próxima leitura resolve
    }

    public void Dispose() => _watcher?.Dispose();
}
```

```csharp
// Credentials/CredentialPipeline.cs
namespace ClaudeWatch.Credentials;

public sealed class CredentialPipeline(
    ICredentialFile file, TokenCache cache, OAuthRefreshClient refresh, Action<string> log)
{
    private static readonly TimeSpan Margin = TimeSpan.FromMinutes(2);
    private readonly SemaphoreSlim _gate = new(1, 1);
    public bool NoCredential { get; private set; }

    public async Task<string?> GetAccessTokenAsync(DateTimeOffset now, CancellationToken ct)
    {
        if (Pick(now) is { } fast) { NoCredential = false; return fast.AccessToken; }

        await _gate.WaitAsync(ct);
        try
        {
            // re-read: alguém pode ter renovado enquanto esperávamos o gate
            if (Pick(now) is { } again) { NoCredential = false; return again.AccessToken; }

            var fileCred = ParseFile();
            if (fileCred?.RefreshToken is not { } rt) { NoCredential = true; return null; }

            var result = await refresh.RefreshAsync(rt, ct);
            if (result.Rejected) { cache.Clear(); NoCredential = true; return null; }
            if (result.RotationDetected)
                log("AVISO: refresh_token rotacionou; modo degradado — NUNCA escrever no arquivo do Claude Code.");
            if (result.Credential is { } c) { cache.Save(c); NoCredential = false; return c.AccessToken; }
            return null;
        }
        finally { _gate.Release(); }
    }

    private OAuthCredential? Pick(DateTimeOffset now) =>
        TokenSelector.PickValid(ParseFile(), cache.Load(), now, Margin);

    private OAuthCredential? ParseFile() =>
        file.ReadOrNull() is { } j ? ClaudeCodeCredentialsParser.TryParse(j) : null;
}
```

**Step 4:** `dotnet test` → PASS (suíte inteira verde). **Step 5:** `git commit -am "feat: pipeline de credencial (read-only no CC, refresh oportunista, cache DPAPI)"`

---

### Task 13: UsagePoller

**Files:** Create: `src/ClaudeWatch/Core/UsagePoller.cs` · Test: `tests/ClaudeWatch.Tests/UsagePollerTests.cs`

**Step 1: teste que falha** (testar `TickAsync`, não o loop)

```csharp
using ClaudeWatch.Core;
using Xunit;

public class UsagePollerTests
{
    [Fact]
    public async Task Sucesso_publica_Ok()
    {
        UsageSnapshot? published = null;
        var p = new UsagePoller(
            getToken: _ => Task.FromResult<string?>("AT"),
            fetch: (_, _) => Task.FromResult(Snapshots.Of(1, 2, 3)),
            publish: s => published = s, log: _ => { });
        await p.TickAsync(default);
        Assert.Equal(SnapshotState.Ok, published!.State);
    }

    [Fact]
    public async Task Sem_token_publica_NoCredential()
    {
        UsageSnapshot? published = null;
        var p = new UsagePoller(_ => Task.FromResult<string?>(null),
            (_, _) => throw new InvalidOperationException("não deveria buscar"),
            s => published = s, _ => { });
        await p.TickAsync(default);
        Assert.Equal(SnapshotState.NoCredential, published!.State);
    }

    [Fact]
    public async Task Falha_preserva_ultimo_snapshot_como_Stale()
    {
        UsageSnapshot? published = null;
        var ok = true;
        var p = new UsagePoller(_ => Task.FromResult<string?>("AT"),
            (_, _) => ok ? Task.FromResult(Snapshots.Of(42, 78, 96)) : throw new HttpRequestException(),
            s => published = s, _ => { });
        await p.TickAsync(default);
        ok = false;
        await p.TickAsync(default);
        Assert.Equal(SnapshotState.Stale, published!.State);
        Assert.Equal(96, published.Opus.Pct); // dados antigos preservados
    }
}
```

**Step 2:** FAIL. **Step 3:**

```csharp
namespace ClaudeWatch.Core;

public sealed class UsagePoller(
    Func<CancellationToken, Task<string?>> getToken,
    Func<string, CancellationToken, Task<UsageSnapshot>> fetch,
    Action<UsageSnapshot> publish,
    Action<string> log)
{
    public BackoffPolicy Backoff { get; } = new();
    private UsageSnapshot? _last;

    public async Task RunAsync(TimeSpan interval, CancellationToken ct)
    {
        while (!ct.IsCancellationRequested)
        {
            await TickAsync(ct);
            try { await Task.Delay(Backoff.NextDelay(interval), ct); }
            catch (OperationCanceledException) { break; }
        }
    }

    public async Task TickAsync(CancellationToken ct)
    {
        try
        {
            var token = await getToken(ct);
            if (token is null) { publish(WithState(SnapshotState.NoCredential)); return; }
            _last = await fetch(token, ct);
            Backoff.Success();
            publish(_last);
        }
        catch (Exception ex)
        {
            log($"tick: {ex.GetType().Name}: {ex.Message}");
            Backoff.Failure();
            publish(WithState(SnapshotState.Stale));
        }
    }

    private UsageSnapshot WithState(SnapshotState st) =>
        (_last ?? Empty()) with { State = st };

    private static UsageSnapshot Empty() => new(
        new Meter("Sessão 5h", 0, null), new Meter("Semana", 0, null),
        new Meter("Opus", 0, null), DateTimeOffset.UtcNow, SnapshotState.Stale);
}
```

**Step 4:** PASS. **Step 5:** `git commit -am "feat: poller com estados Ok/Stale/NoCredential e backoff"`

> O núcleo testável termina aqui. Tasks 14–21 são UI/Win32: sem TDD, cada uma fecha com **verificação manual explícita** + commit. Os dados visuais vêm de um gateway fake (showroom: 42/78/96) até os TODO(carlos) serem preenchidos.

---

### Task 14: Tray — render GDI e controller

**Files:** Create: `src/ClaudeWatch/Tray/IconRenderer.cs`, `src/ClaudeWatch/Tray/TrayController.cs`, `src/ClaudeWatch/Infrastructure/Autostart.cs`, `src/ClaudeWatch/Infrastructure/FileLogger.cs`

**Step 1: `IconRenderer` — o ritual anti-leak é o ponto inegociável**

```csharp
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Text;
using System.Runtime.InteropServices;
using ClaudeWatch.Core;

namespace ClaudeWatch.Tray;

public static class IconRenderer
{
    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool DestroyIcon(IntPtr handle);

    public static Icon Render(Meter worst)
    {
        using var bmp = new Bitmap(32, 32);
        using var g = Graphics.FromImage(bmp);
        g.SmoothingMode = SmoothingMode.AntiAlias;
        g.TextRenderingHint = TextRenderingHint.AntiAliasGridFit; // ClearType não renderiza sobre alpha

        var color = ColorTranslator.FromHtml(ZoneColors.Hex(worst.Zone));
        var text = worst.Pct.ToString();
        using var font = new Font("Segoe UI", worst.Pct >= 100 ? 14f : 19f, FontStyle.Bold, GraphicsUnit.Pixel);
        using var brush = new SolidBrush(color);
        var size = g.MeasureString(text, font);
        g.DrawString(text, font, brush, (32 - size.Width) / 2f, (32 - size.Height) / 2f);

        var hIcon = bmp.GetHicon();
        try
        {
            using var tmp = Icon.FromHandle(hIcon);
            return (Icon)tmp.Clone(); // cópia gerenciada independente do handle nativo
        }
        finally
        {
            DestroyIcon(hIcon); // sem isso: +1 GDI handle por refresh → morte em dias
        }
    }
}
```

**Step 2: `TrayController`**

```csharp
using System.Windows.Forms;
using ClaudeWatch.Core;

namespace ClaudeWatch.Tray;

public sealed class TrayController : IDisposable
{
    private readonly NotifyIcon _icon;
    private System.Drawing.Icon? _current;
    private readonly ToolStripMenuItem _lock, _autostart, _skinAneis, _skinLed;

    public event Action? ToggleWidget, RefreshNow, ExitApp;
    public event Action<bool>? LockChanged, AutostartChanged;
    public event Action<string>? SkinChanged;

    public TrayController(bool locked, bool autostart, string skin)
    {
        _lock = new ToolStripMenuItem("Travar widget") { CheckOnClick = true, Checked = locked };
        _lock.CheckedChanged += (_, _) => LockChanged?.Invoke(_lock.Checked);

        _autostart = new ToolStripMenuItem("Iniciar com o Windows") { CheckOnClick = true, Checked = autostart };
        _autostart.CheckedChanged += (_, _) => AutostartChanged?.Invoke(_autostart.Checked);

        _skinAneis = new ToolStripMenuItem("Anéis") { Checked = skin == "Aneis" };
        _skinLed = new ToolStripMenuItem("LED") { Checked = skin == "Led" };
        _skinAneis.Click += (_, _) => SelectSkin("Aneis");
        _skinLed.Click += (_, _) => SelectSkin("Led");
        var estilo = new ToolStripMenuItem("Estilo");
        estilo.DropDownItems.AddRange([_skinAneis, _skinLed]);

        var menu = new ContextMenuStrip();
        menu.Items.Add("Mostrar/ocultar widget", null, (_, _) => ToggleWidget?.Invoke());
        menu.Items.Add(_lock);
        menu.Items.Add(estilo);
        menu.Items.Add("Atualizar agora", null, (_, _) => RefreshNow?.Invoke());
        menu.Items.Add(_autostart);
        menu.Items.Add(new ToolStripSeparator());
        menu.Items.Add("Sair", null, (_, _) => ExitApp?.Invoke());

        _icon = new NotifyIcon { Visible = true, ContextMenuStrip = menu, Text = "ClaudeWatch" };
        _icon.DoubleClick += (_, _) => ToggleWidget?.Invoke();
    }

    private void SelectSkin(string skin)
    {
        _skinAneis.Checked = skin == "Aneis";
        _skinLed.Checked = skin == "Led";
        SkinChanged?.Invoke(skin);
    }

    public void Update(UsageSnapshot s)
    {
        var next = IconRenderer.Render(s.Worst);
        _icon.Icon = next;
        _icon.Text = TooltipFormatter.Format(s);
        _current?.Dispose();
        _current = next;
    }

    public void Dispose() { _icon.Visible = false; _icon.Dispose(); _current?.Dispose(); }
}
```

**Step 3: `Autostart` e `FileLogger`**

```csharp
// Infrastructure/Autostart.cs
using Microsoft.Win32;

namespace ClaudeWatch.Infrastructure;

public static class Autostart
{
    private const string Key = @"Software\Microsoft\Windows\CurrentVersion\Run";
    private const string Name = "ClaudeWatch";

    public static bool IsEnabled()
    { using var k = Registry.CurrentUser.OpenSubKey(Key); return k?.GetValue(Name) is not null; }

    public static void Set(bool enabled)
    {
        using var k = Registry.CurrentUser.CreateSubKey(Key);
        if (enabled) k.SetValue(Name, $"\"{Environment.ProcessPath}\"");
        else k.DeleteValue(Name, throwOnMissingValue: false);
    }
}
```

```csharp
// Infrastructure/FileLogger.cs
namespace ClaudeWatch.Infrastructure;

public sealed class FileLogger(string dir)
{
    private readonly string _path = Path.Combine(dir, "claudewatch.log");
    private readonly Lock _lock = new();

    public void Log(string msg)
    {
        try
        {
            lock (_lock)
            {
                Directory.CreateDirectory(dir);
                if (File.Exists(_path) && new FileInfo(_path).Length > 1_000_000)
                    File.Move(_path, _path + ".1", overwrite: true);
                File.AppendAllText(_path, $"{DateTimeOffset.Now:O} {msg}{Environment.NewLine}");
            }
        }
        catch { /* logging nunca derruba o app */ }
    }
}
```

**Step 4 (verificação manual):** ainda sem janela — adicionar em `App.OnStartup` um stub temporário criando `TrayController` e chamando `Update(Snapshots.Of(42, 78, 96))` (mover `Snapshots` para `src/ClaudeWatch/Core/` como helper `Debug`? Não: duplicar os 3 valores inline). `dotnet run` → ícone no tray (overflow do Win11) mostrando **96** em vermelho; tooltip `5h 42% (reset --) · Sem 78% · Opus 96%`; menu completo; Sair encerra. Task Manager: GDI objects estável após vários `Update` (testar com botão "Atualizar agora" ligado a `Update`).

**Step 5:** `git commit -am "feat: tray com ícone GDI dinâmico, menu e autostart"`

---

### Task 15: WidgetWindow — shell Win32

**Files:** Create: `src/ClaudeWatch/Widget/WidgetWindow.xaml(.cs)`, `src/ClaudeWatch/Widget/WindowInterop.cs`

**Step 1: XAML do shell (glass vazio)**

```xml
<Window x:Class="ClaudeWatch.Widget.WidgetWindow"
        xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation"
        xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
        WindowStyle="None" AllowsTransparency="True" Background="Transparent"
        Topmost="True" ShowInTaskbar="False" ResizeMode="NoResize"
        SizeToContent="WidthAndHeight" ShowActivated="False">
  <Border CornerRadius="12" Background="#CC1E1E1E"
          BorderBrush="#1AFFFFFF" BorderThickness="1" Padding="14,14,14,12"
          MouseLeftButtonDown="OnDragStart">
    <Border.Effect>
      <DropShadowEffect BlurRadius="34" ShadowDepth="6" Direction="270" Opacity="0.4"/>
    </Border.Effect>
    <ContentControl x:Name="Body"/>
  </Border>
</Window>
```

**Step 2: `WindowInterop` (ex-styles + click-through)**

```csharp
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;

namespace ClaudeWatch.Widget;

public static class WindowInterop
{
    private const int GWL_EXSTYLE = -20;
    private const long WS_EX_TOOLWINDOW = 0x80, WS_EX_NOACTIVATE = 0x08000000,
                       WS_EX_TRANSPARENT = 0x20, WS_EX_LAYERED = 0x80000;

    [DllImport("user32.dll")] private static extern long GetWindowLongPtr(IntPtr h, int i);
    [DllImport("user32.dll")] private static extern long SetWindowLongPtr(IntPtr h, int i, long v);

    public static void ApplyWidgetStyles(Window w)
    {
        var h = new WindowInteropHelper(w).Handle;
        SetWindowLongPtr(h, GWL_EXSTYLE,
            GetWindowLongPtr(h, GWL_EXSTYLE) | WS_EX_TOOLWINDOW | WS_EX_NOACTIVATE);
    }

    public static void SetClickThrough(Window w, bool on)
    {
        var h = new WindowInteropHelper(w).Handle;
        var ex = GetWindowLongPtr(h, GWL_EXSTYLE);
        ex = on ? ex | WS_EX_TRANSPARENT | WS_EX_LAYERED : ex & ~WS_EX_TRANSPARENT;
        SetWindowLongPtr(h, GWL_EXSTYLE, ex);
    }
}
```

**Step 3: code-behind (drag, clamp, persistência de posição)**

```csharp
using System.Windows;
using System.Windows.Input;
using ClaudeWatch.Infrastructure;

namespace ClaudeWatch.Widget;

public partial class WidgetWindow : Window
{
    private readonly SettingsStore _store;
    private bool _locked;

    public WidgetWindow(SettingsStore store, Settings s)
    {
        InitializeComponent();
        _store = store;
        _locked = s.Locked;
        SourceInitialized += (_, _) =>
        {
            WindowInterop.ApplyWidgetStyles(this);
            WindowInterop.SetClickThrough(this, _locked);
        };
        Loaded += (_, _) => Restore(s);
    }

    public void SetLocked(bool locked)
    { _locked = locked; WindowInterop.SetClickThrough(this, locked); }

    public void ReassertTopmost() { Topmost = false; Topmost = true; }

    private void OnDragStart(object sender, MouseButtonEventArgs e)
    {
        if (_locked || e.LeftButton != MouseButtonState.Pressed) return;
        DragMove();
        var s = _store.Load();
        _store.Save(s with { PosX = Left, PosY = Top });
    }

    private void Restore(Settings s)
    {
        var x = s.PosX ?? SystemParameters.WorkArea.Right - ActualWidth - 24;
        var y = s.PosY ?? 24;
        Left = Math.Clamp(x, SystemParameters.VirtualScreenLeft,
            SystemParameters.VirtualScreenLeft + SystemParameters.VirtualScreenWidth - ActualWidth);
        Top = Math.Clamp(y, SystemParameters.VirtualScreenTop,
            SystemParameters.VirtualScreenTop + SystemParameters.VirtualScreenHeight - ActualHeight);
    }
}
```

**Step 4 (verificação manual):** instanciar no stub do `OnStartup` e `Show()`. Card de vidro vazio aparece; arrasta e a posição sobrevive a restart; some do Alt+Tab; clicar nele não rouba foco do editor ativo.

**Step 5:** `git commit -am "feat: shell da WidgetWindow com estilos Win32 e posição persistida"`

---

### Task 16: ViewModel e composition root

**Files:** Create: `src/ClaudeWatch/Widget/WidgetViewModel.cs` · Modify: `src/ClaudeWatch/App.xaml.cs` (substituir o stub)

**Step 1: ViewModel**

```csharp
using System.ComponentModel;
using ClaudeWatch.Core;

namespace ClaudeWatch.Widget;

public sealed class WidgetViewModel : INotifyPropertyChanged
{
    private UsageSnapshot? _snapshot;
    private string _skin = "Aneis";
    public event PropertyChangedEventHandler? PropertyChanged;

    public UsageSnapshot? Snapshot
    { get => _snapshot; set { _snapshot = value; Raise(nameof(Snapshot)); } }

    public string Skin
    { get => _skin; set { _skin = value; Raise(nameof(Skin)); } }

    private void Raise(string n) => PropertyChanged?.Invoke(this, new(n));
}
```

**Step 2: composition root (`App.xaml.cs` completo)**

```csharp
using System.Net.Http;
using System.Windows;
using ClaudeWatch.Core;
using ClaudeWatch.Credentials;
using ClaudeWatch.Infrastructure;
using ClaudeWatch.Tray;
using ClaudeWatch.Widget;

namespace ClaudeWatch;

public partial class App : Application
{
    private TrayController? _tray;
    private WidgetWindow? _window;
    private FileCredentialFile? _credFile;
    private CancellationTokenSource? _cts;

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);
        var log = new FileLogger(AppPaths.LogsDir);
        var store = new SettingsStore(AppPaths.BaseDir);
        var settings = store.Load();

        var home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        _credFile = new FileCredentialFile(CredentialPaths.Resolve(home, settings));
        var http = new HttpClient();
        var pipeline = new CredentialPipeline(_credFile, new TokenCache(AppPaths.BaseDir),
            new OAuthRefreshClient(http), log.Log);
        var usage = new UsageClient(http);

        var vm = new WidgetViewModel { Skin = settings.Skin };
        _window = new WidgetWindow(store, settings) { DataContext = vm };
        _tray = new TrayController(settings.Locked, Autostart.IsEnabled(), settings.Skin);

        var poller = new UsagePoller(
            getToken: ct => pipeline.GetAccessTokenAsync(DateTimeOffset.UtcNow, ct),
            fetch: usage.FetchAsync,
            publish: s => Dispatcher.Invoke(() =>
            { vm.Snapshot = s; _tray.Update(s); _window.ReassertTopmost(); }),
            log: log.Log);

        _cts = new CancellationTokenSource();
        _credFile.Changed += () => _ = poller.TickAsync(_cts.Token);
        _tray.RefreshNow += () => _ = poller.TickAsync(_cts.Token);
        _tray.ToggleWidget += () =>
        { if (_window.IsVisible) _window.Hide(); else _window.Show(); };
        _tray.LockChanged += locked =>
        { _window.SetLocked(locked); store.Save(store.Load() with { Locked = locked }); };
        _tray.SkinChanged += skin =>
        { vm.Skin = skin; store.Save(store.Load() with { Skin = skin }); };
        _tray.AutostartChanged += Autostart.Set;
        _tray.ExitApp += () => { _cts.Cancel(); Shutdown(); };

        _window.Show();
        _ = poller.RunAsync(TimeSpan.FromSeconds(Math.Max(15, settings.IntervalSeconds)), _cts.Token);
    }

    protected override void OnExit(ExitEventArgs e)
    {
        _cts?.Cancel(); _tray?.Dispose(); _credFile?.Dispose();
        base.OnExit(e);
    }
}
```

**Step 3 (manual):** `dotnet run` — sem os endpoints reais, o poller falha e publica `Stale` vazio (esperado). Tooltip atualiza, log em `%AppData%\ClaudeWatch\logs` registra os ticks.

**Step 4:** `git commit -am "feat: composition root ligando poller, tray e janela"`

---

### Task 17: Skin A — três anéis

**Files:** Create: `src/ClaudeWatch/Widget/Converters.cs`, `src/ClaudeWatch/Widget/Skins.xaml` (ResourceDictionary, merged no `App.xaml`) · Modify: `WidgetWindow.xaml` (Body → ContentControl com template por Skin)

**Step 1: converters**

```csharp
using System.Globalization;
using System.Windows.Data;
using System.Windows.Media;
using ClaudeWatch.Core;

namespace ClaudeWatch.Widget;

public sealed class PctToBrushConverter : IValueConverter
{
    public object Convert(object v, Type t, object p, CultureInfo c) =>
        (SolidColorBrush)new BrushConverter().ConvertFromString(
            ZoneColors.Hex(ZoneRules.From(System.Convert.ToInt32(v))))!;
    public object ConvertBack(object v, Type t, object p, CultureInfo c) => throw new NotSupportedException();
}

public sealed class PctToRingGeometryConverter : IValueConverter
{
    // anel 56px, stroke 6 → r=25, centro (28,28), início no topo, sentido horário
    public object Convert(object v, Type t, object p, CultureInfo c)
    {
        var pct = Math.Clamp(System.Convert.ToDouble(v), 0, 100);
        var ang = Math.Min(pct / 100.0 * 360.0, 359.9) * Math.PI / 180.0;
        const double r = 25, cx = 28, cy = 28;
        var fig = new PathFigure { StartPoint = new(cx, cy - r), IsClosed = false };
        fig.Segments.Add(new ArcSegment(
            new(cx + r * Math.Sin(ang), cy - r * Math.Cos(ang)),
            new(r, r), 0, pct > 50, SweepDirection.Clockwise, true));
        var geo = new PathGeometry { Figures = { fig } };
        geo.Freeze();
        return geo;
    }
    public object ConvertBack(object v, Type t, object p, CultureInfo c) => throw new NotSupportedException();
}

public sealed class LedSegmentsConverter : IValueConverter
{
    public object Convert(object v, Type t, object p, CultureInfo c) =>
        LedScale.Build(System.Convert.ToInt32(v));
    public object ConvertBack(object v, Type t, object p, CultureInfo c) => throw new NotSupportedException();
}
```

**Step 2: `Skins.xaml` — template dos anéis**

```xml
<ResourceDictionary xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation"
                    xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
                    xmlns:w="clr-namespace:ClaudeWatch.Widget">
  <w:PctToBrushConverter x:Key="PctBrush"/>
  <w:PctToRingGeometryConverter x:Key="PctRing"/>
  <w:LedSegmentsConverter x:Key="LedSegs"/>

  <DataTemplate x:Key="MeterRing">
    <StackPanel Width="76" HorizontalAlignment="Center">
      <Grid Width="56" Height="56" HorizontalAlignment="Center">
        <Ellipse Stroke="#24FFFFFF" StrokeThickness="6" Margin="3"/>
        <Path Stroke="{Binding Pct, Converter={StaticResource PctBrush}}"
              StrokeThickness="6" StrokeStartLineCap="Round" StrokeEndLineCap="Round"
              Data="{Binding Pct, Converter={StaticResource PctRing}}"/>
        <TextBlock Text="{Binding Pct, StringFormat={}{0}%}" Foreground="White"
                   FontFamily="Segoe UI" FontSize="14" FontWeight="SemiBold"
                   HorizontalAlignment="Center" VerticalAlignment="Center"/>
      </Grid>
      <TextBlock Text="{Binding Label}" Foreground="#BDFFFFFF" FontSize="10.5"
                 HorizontalAlignment="Center" Margin="0,7,0,0"/>
    </StackPanel>
  </DataTemplate>

  <DataTemplate x:Key="SkinAneis">
    <StackPanel Orientation="Horizontal" Width="236">
      <ContentControl Content="{Binding Snapshot.FiveHour}" ContentTemplate="{StaticResource MeterRing}"/>
      <ContentControl Content="{Binding Snapshot.Week}" ContentTemplate="{StaticResource MeterRing}"/>
      <ContentControl Content="{Binding Snapshot.Opus}" ContentTemplate="{StaticResource MeterRing}"/>
    </StackPanel>
  </DataTemplate>
</ResourceDictionary>
```

**Step 3:** `WidgetWindow.xaml`: `Body` vira

```xml
<ContentControl x:Name="Body" Content="{Binding}">
  <ContentControl.Style>
    <Style TargetType="ContentControl">
      <Setter Property="ContentTemplate" Value="{StaticResource SkinAneis}"/>
    </Style>
  </ContentControl.Style>
</ContentControl>
```

**Step 4 (manual):** com o gateway real ainda em TODO, trocar temporariamente o `fetch` do poller por `(_, _) => Task.FromResult(<snapshot 42/78/96>)` (flag `#if DEBUG`). Rodar: três anéis verde/âmbar/vermelho idênticos ao showroom, % centrado, labels corretos.

**Step 5:** `git commit -am "feat: skin Anéis fiel ao showroom"`

---

### Task 18: Skin E — LED/equalizador

**Files:** Modify: `src/ClaudeWatch/Widget/Skins.xaml`

**Step 1: templates LED** (acrescentar ao dicionário)

```xml
  <DataTemplate x:Key="LedRow">
    <Grid Margin="0,6" Width="240">
      <Grid.ColumnDefinitions>
        <ColumnDefinition Width="48"/><ColumnDefinition Width="*"/><ColumnDefinition Width="30"/>
      </Grid.ColumnDefinitions>
      <TextBlock Text="{Binding Label}" Foreground="#C7FFFFFF" FontSize="10.5" VerticalAlignment="Center"/>
      <ItemsControl Grid.Column="1" ItemsSource="{Binding Pct, Converter={StaticResource LedSegs}}"
                    VerticalAlignment="Center">
        <ItemsControl.ItemsPanel>
          <ItemsPanelTemplate><UniformGrid Rows="1" /></ItemsPanelTemplate>
        </ItemsControl.ItemsPanel>
        <ItemsControl.ItemTemplate>
          <DataTemplate>
            <Border Height="11" Margin="1.5,0" CornerRadius="1.5" x:Name="Seg" Background="#1AFFFFFF"/>
            <DataTemplate.Triggers>
              <MultiDataTrigger>
                <MultiDataTrigger.Conditions>
                  <Condition Binding="{Binding Lit}" Value="True"/>
                  <Condition Binding="{Binding Zone}" Value="Verde"/>
                </MultiDataTrigger.Conditions>
                <Setter TargetName="Seg" Property="Background" Value="#3FB950"/>
              </MultiDataTrigger>
              <MultiDataTrigger>
                <MultiDataTrigger.Conditions>
                  <Condition Binding="{Binding Lit}" Value="True"/>
                  <Condition Binding="{Binding Zone}" Value="Ambar"/>
                </MultiDataTrigger.Conditions>
                <Setter TargetName="Seg" Property="Background" Value="#E8A23D"/>
              </MultiDataTrigger>
              <MultiDataTrigger>
                <MultiDataTrigger.Conditions>
                  <Condition Binding="{Binding Lit}" Value="True"/>
                  <Condition Binding="{Binding Zone}" Value="Vermelho"/>
                </MultiDataTrigger.Conditions>
                <Setter TargetName="Seg" Property="Background" Value="#F85149"/>
              </MultiDataTrigger>
            </DataTemplate.Triggers>
          </DataTemplate>
        </ItemsControl.ItemTemplate>
      </ItemsControl>
      <TextBlock Grid.Column="2" Text="{Binding Pct, StringFormat={}{0}%}" FontSize="11"
                 FontWeight="SemiBold" TextAlignment="Right" VerticalAlignment="Center"
                 Foreground="{Binding Pct, Converter={StaticResource PctBrush}}"/>
    </Grid>
  </DataTemplate>

  <DataTemplate x:Key="SkinLed">
    <StackPanel>
      <ContentControl Content="{Binding Snapshot.FiveHour}" ContentTemplate="{StaticResource LedRow}"/>
      <ContentControl Content="{Binding Snapshot.Week}" ContentTemplate="{StaticResource LedRow}"/>
      <ContentControl Content="{Binding Snapshot.Opus}" ContentTemplate="{StaticResource LedRow}"/>
    </StackPanel>
  </DataTemplate>
```

(Glow do showroom: omitido conscientemente — design doc autoriza borda/cor sólida no lugar de 45 `DropShadowEffect`.)

**Step 2: seleção por skin** — no `Style` do `Body`, adicionar:

```xml
<Style.Triggers>
  <DataTrigger Binding="{Binding Skin}" Value="Led">
    <Setter Property="ContentTemplate" Value="{StaticResource SkinLed}"/>
  </DataTrigger>
</Style.Triggers>
```

**Step 3 (manual):** menu do tray *Estilo → LED*: troca ao vivo; com 42/78/96, a linha do Opus acende 14 segmentos com a ponta vermelha e a do 5h acende 6 (verdes); restart preserva o skin escolhido.

**Step 4:** `git commit -am "feat: skin LED com cor posicional e troca ao vivo"`

---

### Task 19: Estados Stale e NoCredential

**Files:** Modify: `Skins.xaml`, `WidgetWindow.xaml` · Create: `src/ClaudeWatch/Widget/AppUrls.cs`

**Step 1:** `AppUrls.ClaudeCode = "TODO(carlos)"` + handler abrindo via `Process.Start(new ProcessStartInfo(url) { UseShellExecute = true })`.

**Step 2:** envolver o `Body` num `Grid` com: overlay de rodapé visível quando `Snapshot.State == Stale` (`TextBlock` "⚠ atualizado às {Snapshot.CollectedAt:HH:mm}", fundo gradiente preto 18%) e `Opacity 0.55` no corpo (aproximação consciente do grayscale CSS — design doc); painel NoCredential (🔒 + "Faça login no Claude Code" + botão "Conectar conta") substituindo o corpo via `DataTrigger` em `Snapshot.State`. XAML segue o padrão dos triggers da Task 18.

**Step 3 (manual):** forçar os três estados pelo fake do DEBUG (devolver `Stale`/`NoCredential`): visuais corretos nos dois skins; botão abre a URL.

**Step 4:** `git commit -am "feat: estados stale e sem credencial"`

---

### Task 20: Single-instance

**Files:** Modify: `src/ClaudeWatch/App.xaml.cs`

**Step 1:** no topo do `OnStartup`:

```csharp
_mutex = new Mutex(true, @"Global\ClaudeWatch.Widget", out var first);
_showSignal = new EventWaitHandle(false, EventResetMode.AutoReset, @"Global\ClaudeWatch.Show");
if (!first) { _showSignal.Set(); Shutdown(); return; }
var t = new Thread(() =>
{
    while (_showSignal.WaitOne())
        Dispatcher.Invoke(() => { _window?.Show(); _window?.Activate(); });
}) { IsBackground = true };
t.Start();
```

(campos `private Mutex? _mutex; private EventWaitHandle? _showSignal;`; liberar no `OnExit`.)

**Step 2 (manual):** abrir o exe duas vezes → segunda instância morre e a janela da primeira reaparece se estava oculta.

**Step 3:** `git commit -am "feat: instância única com sinal de show"`

---

### Task 21: TODO(carlos), publish e smoke de distribuição

**Files:** Modify: `OAuthConstants.cs`, `UsageResponseParser.cs` (+fixture), `ClaudeCodeCredentialsParser.cs` se necessário, `AppUrls.cs` · Create: `README-amigos.md`, `publish.ps1`

**Step 1:** preencher os 4 TODO(carlos) com os valores do parity do CORTEX; rodar `dotnet test` → suíte verde (ajustar fixture de usage se o shape real divergir). Remover o fake `#if DEBUG` do fetch.

**Step 2: `publish.ps1`**

```powershell
dotnet publish src/ClaudeWatch -c Release -r win-x64 --self-contained `
  /p:PublishSingleFile=true /p:IncludeNativeLibrariesForSelfExtract=true `
  /p:PublishReadyToRun=true /p:PublishTrimmed=false -o publish
```

Esperado: `publish/ClaudeWatch.exe` (~80MB). **Trimming OFF é inegociável** (WPF quebra trimado).

**Step 3: smoke como "amigo":** renomear temporariamente `%AppData%\ClaudeWatch` e rodar o exe publicado → estado NoCredential com CTA; com Claude Code logado → três medidores reais em <90s; expirar o access token na mão (editar `expiresAt` numa cópia? não — apenas aguardar expiração natural ou validar refresh via log) → refresh oportunista loga sucesso e **o `.credentials.json` permanece byte-idêntico** (comparar hash antes/depois: `Get-FileHash`). Autostart liga/desliga conferindo a Run key.

**Step 4: `README-amigos.md`:** requisitos (Windows 10/11, Claude Code logado), aviso SmartScreen ("Mais informações → Executar assim mesmo"), dica do overflow do tray, onde ficam settings/logs, como desinstalar (apagar exe + `%AppData%\ClaudeWatch` + desligar autostart).

**Step 5:** `git commit -am "chore: publish single-file e README de distribuição"` → tag `v0.1.0`.

---

## Execution Handoff

Plano salvo. Duas opções de execução:

1. **Subagent-Driven (mesma sessão)** — REQUIRED SUB-SKILL: superpowers:subagent-driven-development; um subagente fresco por task com review entre tasks.
2. **Parallel Session (sessão separada na worktree)** — REQUIRED SUB-SKILL: superpowers:executing-plans; execução em lote com checkpoints.
