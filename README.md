<h1 align="center">🟢 ClaudeWatch</h1>

<p align="center">
  <b>Português</b> ·
  <a href="README.en.md">English</a> ·
  <a href="README.es.md">Español</a>
</p>

<p align="center">
  <em>Acompanhe o consumo da sua assinatura <b>Claude</b> sem sair do que está fazendo —<br>
  um widget discreto, sempre à vista, no seu desktop Windows.</em>
</p>

<p align="center">
  <img alt="versão" src="https://img.shields.io/github/v/release/carlosdealmeida/claude-watch?label=vers%C3%A3o&color=3FB950">
  <img alt="downloads" src="https://img.shields.io/github/downloads/carlosdealmeida/claude-watch/total?label=downloads&color=4C9AFF">
  <img alt="Windows" src="https://img.shields.io/badge/Windows-10%20%7C%2011-0078D6?logo=windows&logoColor=white">
</p>

<!--
Depois de gerar as imagens (assets/aneis.png e assets/led.png), descomente:
<p align="center">
  <img src="assets/aneis.png" alt="Skin Anéis" width="320">
  <img src="assets/led.png" alt="Skin LED" width="320">
</p>
-->

O **ClaudeWatch** mostra, em tempo real e de relance, quanto você já usou dos seus limites do Claude:

- ⏱️ **Sessão de 5 horas**
- 📅 **Semana**
- 🧠 **Sonnet semanal**

…num cartão flutuante elegante e num ícone colorido ao lado do relógio. Ele lê a credencial do **Claude Code** que você já tem instalado — e **nunca a altera**.

## ✨ Recursos

- 🎨 Dois estilos: **Anéis** e **LED** — troque com um clique
- 🔢 Ícone no tray com o medidor mais crítico, na cor da zona
- 🚦 Cores por nível: 🟢 verde (&lt;70%) · 🟠 âmbar (70–89%) · 🔴 vermelho (≥90%)
- 📌 Sempre no topo, arrastável, com modo **travado** (clica através, não atrapalha)
- 🪟 Inicia com o Windows (opcional)
- 🔔 Avisa quando há uma nova versão
- 📦 Um único `.exe` — sem instalação, sem runtime

## 📥 Instalação

1. Baixe o `ClaudeWatch.exe` na **[última release](https://github.com/carlosdealmeida/claude-watch/releases/latest)**.
2. Dê dois cliques.
3. Na primeira vez, o Windows mostra um aviso azul (**SmartScreen**) porque o app não é assinado → clique em **"Mais informações" → "Executar assim mesmo"**.

> 💡 O Windows 11 esconde ícones novos na setinha `^` perto do relógio. Arraste o do ClaudeWatch para fora se quiser deixá-lo sempre à vista.

**Requisitos:** Windows 10 ou 11 · **Claude Code** instalado e logado (`claude` no terminal).

## 🖱️ Como usar

- **Duplo clique** no ícone: mostra/oculta o widget
- **Botão direito** no ícone abre o menu:
  - *Mostrar/ocultar widget* · *Travar widget* · *Estilo: Anéis / LED*
  - *Atualizar agora* · *Iniciar com o Windows* · *Sair*
- **Arraste** o cartão quando destravado — a posição é lembrada

## 🚦 Estados

- **Cinza + "⚠ atualizado às HH:mm"** — sem internet ou API fora; mostra o último dado conhecido.
- **🔒 "Faça login no Claude Code"** — sem credencial válida; faça login (`claude`) e o widget volta sozinho.

## 🔒 Privacidade e segurança

- **Lê** o `.credentials.json` do Claude Code, mas **nunca escreve** nele — invariante absoluta.
- Usa a **mesma API** que o comando `/usage` do Claude Code.
- O cache do token fica protegido por **DPAPI** (criptografia do Windows, por usuário).
- **Sem telemetria**: nada vai para terceiros — apenas a chamada à API da Anthropic.

## ⚠️ Limitações e avisos

O ClaudeWatch é um projeto **não-oficial** e **não é afiliado à Anthropic**. Ele se apoia na credencial e na API do Claude Code, então:

- Pode **parar de funcionar** se a Anthropic mudar a API (sem aviso).
- Faça uso **pessoal e moderado** — consultas muito frequentes podem ser limitadas (HTTP 429).
- Use **por sua conta e risco**.

## 🔄 Atualizações

A cada algumas horas o app verifica se há versão nova e avisa pelo ícone do tray, por um balão do Windows e por um rodapé no próprio widget — é só clicar para abrir a página de download.

## 🗂️ Arquivos e desinstalação

- Configurações: `%AppData%\ClaudeWatch\settings.json` · Logs: `%AppData%\ClaudeWatch\logs\`
- Para remover: feche pelo menu (*Sair*), desmarque *Iniciar com o Windows*, e apague o `.exe` junto da pasta `%AppData%\ClaudeWatch`.

## 🛠️ Para desenvolvedores

Feito com **.NET 10 + WPF**. Compile com `dotnet build ClaudeWatch.slnx` e rode os testes com `dotnet test ClaudeWatch.slnx`.

---

<p align="center"><sub>Projeto pessoal, sem fins lucrativos. Claude e Anthropic são marcas da Anthropic — este projeto não é afiliado nem endossado por ela.</sub></p>
