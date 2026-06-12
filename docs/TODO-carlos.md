# Pré-requisitos bloqueantes — TODO(carlos)

Todo o código está implementado e os 43 testes do núcleo passam usando fakes/fixtures.
**O runtime real depende de 4 valores que só existem no parity do CORTEX.** Enquanto
não forem preenchidos, o app roda, mostra a janela e o tray, mas o poller falha ao
chamar o endpoint placeholder (`https://TODO/usage`) e fica em estado `Stale`.

Para validar o fluxo sem esses valores, use o **modo showroom** (ver README).

| # | O que falta | Arquivo | Detalhe |
|---|---|---|---|
| 1 | `client_id` público + token endpoint (+ tipo do grant: JSON ou form-urlencoded) | `src/ClaudeWatch/Credentials/OAuthConstants.cs` | Hoje: `ClientId="TODO-client-id"`, `TokenEndpoint="https://TODO/token"`. O grant atual usa JSON (`PostAsJsonAsync` em `OAuthRefreshClient`); se o servidor espera `application/x-www-form-urlencoded`, trocar por `FormUrlEncodedContent`. |
| 2 | Endpoint de usage + shape real da resposta | `OAuthConstants.UsageEndpoint` + `src/ClaudeWatch/Core/UsageResponseParser.cs` | Hoje: `UsageEndpoint="https://TODO/usage"`. O parser assume `{ five_hour, seven_day, seven_day_opus }` cada um com `{ utilization, resets_at }`. Ajustar as chaves ao shape real e atualizar a fixture em `tests/ClaudeWatch.Tests/UsageTests.cs`. |
| 3 | Nomes exatos dos campos do `.credentials.json` | `src/ClaudeWatch/Credentials/ClaudeCodeCredentialsParser.cs` | Hoje assume `claudeAiOauth.accessToken/refreshToken/expiresAt` (expiresAt em ms epoch). Confirmar contra o arquivo real do Claude Code. |
| 4 | URL do botão "Conectar conta" | `src/ClaudeWatch/Widget/AppUrls.cs` | Hoje: `ClaudeCode="https://claude.ai/login"` (placeholder plausível). Confirmar a página correta de login/conta. |

## Depois de preencher

1. Atualizar os 4 arquivos acima.
2. Ajustar a fixture de usage se o shape divergir → `dotnet test` deve ficar verde.
3. Smoke real: com o Claude Code logado, os três medidores devem aparecer em < 90s.
   Verificar no log (`%AppData%\ClaudeWatch\logs\claudewatch.log`) que o refresh,
   quando ocorre, **não altera** o `.credentials.json` (comparar `Get-FileHash`
   antes/depois — invariante absoluta do design).
4. O modo showroom (`CLAUDEWATCH_SHOWROOM`) pode permanecer como ferramenta de QA.
