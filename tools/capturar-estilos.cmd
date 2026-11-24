@echo off
rem Prints dos estilos para o README (modo demo, sem dados reais).
rem Uso: tools\capturar-estilos.cmd [-Assets] [-Fundo '#4C566A'] [-Margem 18]
rem Roda num processo novo do PowerShell; veja os detalhes em capturar-estilos.ps1.
pwsh -NoProfile -File "%~dp0capturar-estilos.ps1" %*
