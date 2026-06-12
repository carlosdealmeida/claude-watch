# Pré-requisitos de runtime — RESOLVIDOS

Os 4 valores que faltavam foram encontrados na **fonte primária**: o código do
**Claude Code CLI v2.1.88 descompilado**, versionado em `C:\@work\MyProjects\claude-code-dotnet`
(o "CORTEX"), e seu port em `C:\@work\MyProjects\ATLAS`. Todos já estão no código.

| # | Valor | Onde foi aplicado | Fonte |
|---|---|---|---|
| 1 | `client_id = 9d1c250a-e61b-44d9-88ed-5944d1962f5e`; token endpoint `https://platform.claude.com/v1/oauth/token`; **grant `application/x-www-form-urlencoded`** | `OAuthConstants.cs`, `OAuthRefreshClient.cs` | `claude-code-dotnet/.../OAuthConstants.cs`, `ATLAS/.../oauth/{constants,token_exchange}.py` |
| 2 | usage endpoint `GET https://api.anthropic.com/api/oauth/usage` (Bearer + `anthropic-beta: oauth-2025-04-20`); shape `{ five_hour, seven_day, seven_day_opus, ... }` com buckets podendo ser `null` | `OAuthConstants.cs`, `UsageClient.cs`, `UsageResponseParser.cs` | `claude-code-2-1-88/src/services/api/usage.ts` + fixtures `ATLAS/flutter_app/test/fixtures/claude_usage_*.json` |
| 3 | campos `claudeAiOauth.accessToken/refreshToken/expiresAt` (ms epoch) — já estava correto | `ClaudeCodeCredentialsParser.cs` | `POCOAUTH/claude_oauth.py`, formato do `~/.claude/.credentials.json` |
| 4 | URL de login `https://claude.ai/login` — já estava correto | `AppUrls.cs` | `ATLAS/.../claude_login_webview_screen.dart` |

## Detalhes que importam

- **Por que Bearer e não cookies web:** o ATLAS busca usage em `claude.ai/api/organizations/{org_id}/usage`
  via cookies de sessão (sessionKey + Cloudflare) — caminho que **não** serve ao ClaudeWatch (ele só tem o
  token OAuth do CLI). O CLI oficial usa `api.anthropic.com/api/oauth/usage` **só com Bearer**
  (`usage.ts` + `http.ts` provam: nenhum cookie é montado). É esse o caminho que o ClaudeWatch replica.
- **Headers obrigatórios no usage:** `Authorization: Bearer <token>` + `anthropic-beta: oauth-2025-04-20`
  + `User-Agent: claude-cli/2.1.90 (external, cli)`. Sem o `anthropic-beta` o endpoint recusa; sem o
  `User-Agent` o Cloudflare pode bloquear (erro 1010).
- **Buckets null:** `seven_day_opus` (e outros) vêm `null` em planos sem aquela cota — o parser mapeia para 0%.

## Validação pendente (não automatizável aqui)

Falta o **smoke contra a API real**: com o Claude Code logado, confirmar que
`GET api.anthropic.com/api/oauth/usage` devolve 200 e os três medidores aparecem. Isso exige uma
chamada de rede autenticada com o token real — fazer manualmente ou autorizar a execução do app/da chamada.
A invariante de segurança (não escrever no `.credentials.json`) já foi confirmada por hash em smoke anterior.
