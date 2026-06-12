#requires -Version 7
# Publica o ClaudeWatch como um unico .exe self-contained (~80MB), sem instalador
# e sem runtime instalado. Trimming OFF e inegociavel: WPF quebra em runtime quando trimado.
$ErrorActionPreference = 'Stop'
$root = $PSScriptRoot

dotnet publish "$root\src\ClaudeWatch" -c Release -r win-x64 --self-contained `
  /p:PublishSingleFile=true `
  /p:IncludeNativeLibrariesForSelfExtract=true `
  /p:PublishReadyToRun=true `
  /p:PublishTrimmed=false `
  -o "$root\publish"

$exe = Join-Path $root 'publish\ClaudeWatch.exe'
if (Test-Path $exe) {
    $mb = [math]::Round((Get-Item $exe).Length / 1MB, 1)
    Write-Host "OK: $exe ($mb MB)" -ForegroundColor Green
} else {
    Write-Error "Falha: $exe nao foi gerado."
}
