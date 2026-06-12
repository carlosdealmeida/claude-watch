# ClaudeWatch — guia rápido

Um widget de mesa que mostra o quanto você já gastou da sua assinatura do Claude:
**sessão de 5h**, **semana** e **Sonnet semanal** — num cartão flutuante sempre visível
e num ícone ao lado do relógio.

## Requisitos

- **Windows 10 ou 11**
- **Claude Code instalado e logado** (o widget lê a credencial dele; nunca a altera).

## Instalação

1. Copie o `ClaudeWatch.exe` para qualquer pasta (ex.: `Documentos\ClaudeWatch`).
2. Dê dois cliques.
3. Na **primeira execução o Windows mostra um aviso azul (SmartScreen)** porque o
   programa não é assinado. Clique em **"Mais informações" → "Executar assim mesmo"**.

> O Windows 11 costuma **esconder ícones novos** na setinha `^` ao lado do relógio.
> Arraste o ícone do ClaudeWatch para fora se quiser deixá-lo sempre à vista.

## Usando

- **Duplo clique no ícone**: mostra/oculta o widget.
- **Botão direito no ícone**: menu com
  - *Mostrar/ocultar widget*
  - *Travar widget* (deixa o cartão "clicável através" — não atrapalha o que está atrás)
  - *Estilo*: **Anéis** ou **LED**
  - *Atualizar agora*
  - *Iniciar com o Windows*
  - *Sair*
- **Arrastar o cartão**: clique e arraste (quando não estiver travado). A posição é lembrada.

As cores: **verde** abaixo de 70%, **âmbar** de 70 a 89%, **vermelho** a partir de 90%.

## Estados

- **Cinza + "⚠ atualizado às HH:mm"**: sem internet ou API fora; mostrando o último dado.
- **🔒 "Faça login no Claude Code"**: a credencial expirou/saiu. Faça login no Claude Code
  e o widget volta sozinho.

## Onde ficam os arquivos

- Configurações: `%AppData%\ClaudeWatch\settings.json`
- Logs: `%AppData%\ClaudeWatch\logs\`
- Cache do token (protegido por DPAPI): `%AppData%\ClaudeWatch\token.bin`

## Desinstalar

1. Menu do ícone → *Iniciar com o Windows* desmarcado (ou apague a entrada `ClaudeWatch`
   em `HKCU\Software\Microsoft\Windows\CurrentVersion\Run`).
2. Feche pelo menu → *Sair*.
3. Apague o `ClaudeWatch.exe` e a pasta `%AppData%\ClaudeWatch`.
