# 🗺️ Roadmap

Direção do **ClaudeWatch**. As prioridades são guiadas pela dor real da comunidade — cada item referencia issues do [`anthropics/claude-code`](https://github.com/anthropics/claude-code/issues) que mostram a demanda. O foco do projeto é **visibilidade e aviso antecipado** do uso; bugs do próprio CLI ficam fora do escopo (o ClaudeWatch só *lê* a credencial e **nunca a altera**).

O acompanhamento detalhado de cada item está nas [issues](https://github.com/carlosdealmeida/claude-watch/issues) e [milestones](https://github.com/carlosdealmeida/claude-watch/milestones).

## ✅ Entregue

### v0.5.0 — Fonte oficial e documentação dos limites
A fonte padrão passa a ser a **statusline do Claude Code**, o caminho documentado: o ClaudeWatch vira a statusline, grava só os limites e não toca no token. A API de uso continua disponível como opção, com aviso. Nova documentação em [LIMITES.md](LIMITES.md), com o formato das duas fontes, a conta do ritmo e receitas de statusline.

### v0.4.0 — Limites dinâmicos e ritmo
O cartão mostra os limites que o plano tem de fato, lidos do array `limits` da resposta: no Ultra verificado, **Semana** e **Fable**, sem a sessão de 5h. Cada medidor projeta, pelo ritmo da janela atual, se acaba antes do reset (`⚠ acaba dom 01:16`) ou mostra quando reseta (`↻ seg 09:00`). Cobre a parte do reset semanal da [#2](https://github.com/carlosdealmeida/claude-watch/issues/2).

Três estilos novos para escolher no tray ([#12](https://github.com/carlosdealmeida/claude-watch/issues/12)): **Anéis** concêntricos, **Barras** com a fatia do Fable dentro da semana ([#11](https://github.com/carlosdealmeida/claude-watch/issues/11)) e **Compacto**, cujo detalhe ao passar o mouse mostra de onde veio o consumo da semana ([#10](https://github.com/carlosdealmeida/claude-watch/issues/10), em parte). O ícone do tray destaca o limite que acaba antes do reset, em âmbar mesmo com percentual baixo.

### v0.3.0 — Reset da sessão de 5h no cartão
Linha `↻ reseta às HH:mm` no rodapé do cartão (ambos os skins), some quando não há reset. Ataca diretamente a confusão de _"quando volta a poder usar"_ (claude-code#6679).

### v0.2.0 / v0.1.0 — Base
Widget flutuante, ícone no tray com o medidor mais crítico, dois skins (Anéis e LED), zonas por cor, leitura da credencial do Claude Code, aviso de nova versão, single-file `.exe`.

## 🎯 v0.6 — Aviso antecipado

Funciona com as duas fontes, inclusive a oficial: só precisa do percentual e do horário do reset.

- **[#2](https://github.com/carlosdealmeida/claude-watch/issues/2) — Contagem regressiva do reset.** `↻ em 2h13` quando falta menos de 24h (mais longe, `↻ seg 09:00`), atualizando sozinha de minuto em minuto. O reset semanal e o ritmo já saíram na v0.4.0 (claude-code#6679, #3626, #4267, #13354).
- **[#1](https://github.com/carlosdealmeida/claude-watch/issues/1) — Notificação proativa ao cruzar zona.** Notificação do Windows quando um medidor passa de 70% / 90%, esgota ou o ritmo diz que acaba antes do reset; uma vez por janela, liga/desliga no tray. Responde à dor nº 1: ser pego de surpresa pelo limite (claude-code#16157, #38335, #45756, #41788, #9424).

## 🎯 v0.7 — Pela API de uso

Só com a API de uso, a fonte opcional e não oficial: a statusline do Claude Code não traz esses dados.

- **[#5](https://github.com/carlosdealmeida/claude-watch/issues/5) — Indicador de "extra usage" ativo.** Sinaliza quando se está em cobrança extra vs cota do plano (claude-code#52467, #47353, #45035). O bucket já chega na resposta.
- **[#10](https://github.com/carlosdealmeida/claude-watch/issues/10) — De onde vem o consumo (o que falta).** `percent_of_limit`, o horário dos dados e o detalhe nos estilos Anéis, Barras e LED.
- **[#13](https://github.com/carlosdealmeida/claude-watch/issues/13) — Campos novos da resposta.** `pace`, `severity`, `is_active` e os valores em dólares, com respostas de contas Max e Pro.
- **[#3](https://github.com/carlosdealmeida/claude-watch/issues/3) — Spike: o endpoint expõe tokens/custo?** Investigação que decide a viabilidade da #4 antes de prometer.
- **[#4](https://github.com/carlosdealmeida/claude-watch/issues/4) — Mostrar tokens/custo.** Números absolutos no tooltip ou modo expandido (claude-code#11008, #9293, #1287). _Depende da #3._

## 🔮 Futuro

- **[#6](https://github.com/carlosdealmeida/claude-watch/issues/6) — Histórico/sparkline de uso.** Tendência ao longo do tempo, com dados locais.
- **[#7](https://github.com/carlosdealmeida/claude-watch/issues/7) — Suporte a múltiplas contas.** Alternar/monitorar mais de uma credencial.

---

<sub>Este roadmap é um guia, não um compromisso de datas. Sugestões? Abra uma [issue](https://github.com/carlosdealmeida/claude-watch/issues/new).</sub>
