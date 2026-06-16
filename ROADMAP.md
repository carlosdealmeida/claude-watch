# 🗺️ Roadmap

Direção do **ClaudeWatch**. As prioridades são guiadas pela dor real da comunidade — cada item referencia issues do [`anthropics/claude-code`](https://github.com/anthropics/claude-code/issues) que mostram a demanda. O foco do projeto é **visibilidade e aviso antecipado** do uso; bugs do próprio CLI ficam fora do escopo (o ClaudeWatch só *lê* a credencial e **nunca a altera**).

O acompanhamento detalhado de cada item está nas [issues](https://github.com/carlosdealmeida/claude-watch/issues) e [milestones](https://github.com/carlosdealmeida/claude-watch/milestones).

## ✅ Entregue

### v0.3.0 — Reset da sessão de 5h no cartão
Linha `↻ reseta às HH:mm` no rodapé do cartão (ambos os skins), some quando não há reset. Ataca diretamente a confusão de _"quando volta a poder usar"_ (claude-code#6679).

### v0.2.0 / v0.1.0 — Base
Widget flutuante, ícone no tray com o medidor mais crítico, dois skins (Anéis e LED), zonas por cor, leitura da credencial do Claude Code, aviso de nova versão, single-file `.exe`.

## 🎯 v0.4 — Aviso antecipado e reset mais rico

- **[#1](https://github.com/carlosdealmeida/claude-watch/issues/1) — Notificação proativa ao cruzar zona.** Toast do Windows (+ som opcional) quando um medidor cruza 70% / 90%, disparado só na transição. Responde à dor nº 1: ser pego de surpresa pelo limite (claude-code#16157, #38335, #45756, #41788, #9424).
- **[#2](https://github.com/carlosdealmeida/claude-watch/issues/2) — Contagem regressiva + reset da semana.** `↻ reseta às 14:30 (em 2h13)` e o reset semanal. Estende a v0.3.0 (claude-code#6679, #3626, #4267, #13354).

## 🎯 v0.5 — Números e cobrança extra

- **[#3](https://github.com/carlosdealmeida/claude-watch/issues/3) — Spike: o endpoint expõe tokens/custo?** Investigação que decide a viabilidade da #4 antes de prometer.
- **[#4](https://github.com/carlosdealmeida/claude-watch/issues/4) — Mostrar tokens/custo.** Números absolutos no tooltip ou modo expandido (claude-code#11008, #9293, #1287). _Depende da #3._
- **[#5](https://github.com/carlosdealmeida/claude-watch/issues/5) — Indicador de "extra usage" ativo.** Sinaliza quando se está em cobrança extra vs cota do plano (claude-code#52467, #47353, #45035). O bucket já chega na resposta.

## 🔮 Futuro

- **[#6](https://github.com/carlosdealmeida/claude-watch/issues/6) — Histórico/sparkline de uso.** Tendência ao longo do tempo, com dados locais.
- **[#7](https://github.com/carlosdealmeida/claude-watch/issues/7) — Suporte a múltiplas contas.** Alternar/monitorar mais de uma credencial.

---

<sub>Este roadmap é um guia, não um compromisso de datas. Sugestões? Abra uma [issue](https://github.com/carlosdealmeida/claude-watch/issues/new).</sub>
