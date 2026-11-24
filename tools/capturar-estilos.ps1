# Fotografa o widget do ClaudeWatch em cada estilo e modo demo (CLAUDEWATCH_SHOWROOM,
# sem dados reais), sobre um fundo liso. São os prints do README.
#
# Rode num processo novo (o tipo auxiliar não recarrega numa sessão já aberta):
#   tools\capturar-estilos.cmd            -> grava em %TEMP%\claudewatch-capturas
#   tools\capturar-estilos.cmd -Assets    -> também copia os do modo "ok" para assets\
#
# Antes: dotnet build ClaudeWatch.slnx -c Release
# Atenção: fecha o ClaudeWatch que estiver aberto, troca o estilo no settings.json
# durante a captura e devolve o arquivo original no fim. O detalhe que o Compacto
# abre ao passar o mouse não é fotografado.
param(
    [string[]]$Modos = @('ultra', 'ok'),
    [string[]]$Estilos = @('Aneis', 'Barras', 'Compacto', 'Led'),
    [string]$Exe = (Join-Path $PSScriptRoot '..\src\ClaudeWatch\bin\Release\net10.0-windows\ClaudeWatch.exe'),
    [string]$Saida = (Join-Path $env:TEMP 'claudewatch-capturas'),
    [string]$Fundo = '#4C566A',   # cor lisa atrás do widget (o cartão é translúcido)
    [int]$Margem = 18,            # px de fundo em volta do cartão
    [switch]$Assets               # copia ok-<estilo>.png para assets\<estilo>.png
)

if (-not (Test-Path $Exe)) {
    throw "Não achei $Exe. Compile antes: dotnet build ClaudeWatch.slnx -c Release"
}

Add-Type -AssemblyName System.Drawing
Add-Type -AssemblyName System.Windows.Forms
Add-Type @'
using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
public static class CapturaJanelas {
    [StructLayout(LayoutKind.Sequential)] public struct RECT { public int L, T, R, B; }
    delegate bool EnumProc(IntPtr h, IntPtr p);
    [DllImport("user32.dll")] static extern bool EnumWindows(EnumProc f, IntPtr p);
    [DllImport("user32.dll")] static extern uint GetWindowThreadProcessId(IntPtr h, out uint pid);
    [DllImport("user32.dll")] static extern bool IsWindowVisible(IntPtr h);
    [DllImport("user32.dll")] static extern bool GetWindowRect(IntPtr h, out RECT r);
    [DllImport("user32.dll")] public static extern bool SetProcessDPIAware();
    [DllImport("user32.dll")] static extern bool SetWindowPos(IntPtr h, IntPtr after, int x, int y, int cx, int cy, uint flags);
    public static IntPtr Ultima;
    public static RECT Maior(uint alvo) {
        RECT melhor = new RECT(); long area = 0; Ultima = IntPtr.Zero;
        EnumWindows((h, p) => {
            uint pid; GetWindowThreadProcessId(h, out pid);
            if (pid != alvo || !IsWindowVisible(h)) return true;
            RECT r; GetWindowRect(h, out r);
            long a = (long)(r.R - r.L) * (r.B - r.T);
            if (a > area) { area = a; melhor = r; Ultima = h; }
            return true;
        }, IntPtr.Zero);
        return melhor;
    }
    // Põe a janela no topo da faixa "sempre no topo", sem mover, redimensionar nem ativar.
    public static void AoTopo(IntPtr h) {
        SetWindowPos(h, new IntPtr(-1), 0, 0, 0, 0, 0x0001 | 0x0002 | 0x0010 | 0x0040);
    }
}
'@
[CapturaJanelas]::SetProcessDPIAware() | Out-Null

$cfgDir = Join-Path $env:APPDATA 'ClaudeWatch'
$cfg = Join-Path $cfgDir 'settings.json'
$backup = if (Test-Path $cfg) { Get-Content $cfg -Raw } else { $null }
New-Item -ItemType Directory -Force $Saida | Out-Null
New-Item -ItemType Directory -Force $cfgDir | Out-Null
# Apaga as capturas da rodada anterior, para não sobrar nenhuma com outra janela atrás.
Get-ChildItem $Saida -Filter *.png | Remove-Item

Get-Process ClaudeWatch -ErrorAction SilentlyContinue | Stop-Process -Force
try {
    foreach ($modo in $Modos) {
        foreach ($estilo in $Estilos) {
            $s = if ($backup) { $backup | ConvertFrom-Json } else { [pscustomobject]@{} }
            $s | Add-Member -Force Skin $estilo
            $s | ConvertTo-Json | Set-Content $cfg -Encoding utf8

            $env:CLAUDEWATCH_SHOWROOM = $modo
            $p = Start-Process $Exe -PassThru
            Start-Sleep -Seconds 3
            $r = [CapturaJanelas]::Maior([uint32]$p.Id)
            $widget = [CapturaJanelas]::Ultima
            if (($r.R - $r.L) -le 0 -or ($r.B - $r.T) -le 0) { Write-Warning "$modo/$estilo sem janela visível"; Stop-Process $p -Force; continue }
            $x = $r.L - $Margem; $y = $r.T - $Margem
            $w = $r.R - $r.L + 2 * $Margem; $h = $r.B - $r.T + 2 * $Margem

            # Fundo liso "sempre no topo" (senão o Windows o deixa atrás da janela ativa)
            # e o widget reposto por cima dele.
            $f = New-Object System.Windows.Forms.Form
            $f.FormBorderStyle = 'None'; $f.ShowInTaskbar = $false; $f.StartPosition = 'Manual'
            $f.TopMost = $true
            $f.BackColor = [System.Drawing.ColorTranslator]::FromHtml($Fundo)
            $f.Location = New-Object System.Drawing.Point $x, $y
            $f.Size = New-Object System.Drawing.Size $w, $h
            $f.Show(); [System.Windows.Forms.Application]::DoEvents()
            $f.Location = New-Object System.Drawing.Point $x, $y   # Show() pode reposicionar
            [CapturaJanelas]::AoTopo($widget)
            [System.Windows.Forms.Application]::DoEvents()
            Start-Sleep -Milliseconds 500

            $bmp = New-Object System.Drawing.Bitmap $w, $h
            $g = [System.Drawing.Graphics]::FromImage($bmp)
            $g.CopyFromScreen($x, $y, 0, 0, $bmp.Size)
            $f.Close(); $f.Dispose()

            # Trava: a borda inteira tem de ser o fundo; senão vazou outra janela e a captura é descartada.
            $cor = [System.Drawing.ColorTranslator]::FromHtml($Fundo).ToArgb()
            $pontos = @(@(1,1), @(($w-2),1), @(1,($h-2)), @(($w-2),($h-2)), @([int]($w/2),1), @([int]($w/2),($h-2)), @(1,[int]($h/2)), @(($w-2),[int]($h/2)))
            $vazou = $pontos | Where-Object { $bmp.GetPixel($_[0], $_[1]).ToArgb() -ne $cor }
            if ($vazou) {
                Write-Warning "$modo/$estilo descartada: o fundo não ficou atrás do widget"
            } else {
                $arq = Join-Path $Saida "$modo-$($estilo.ToLower()).png"
                $bmp.Save($arq, [System.Drawing.Imaging.ImageFormat]::Png)
                Write-Host "ok  $arq  (${w}x${h})"
            }
            $g.Dispose(); $bmp.Dispose()
            Stop-Process $p -Force
            Start-Sleep -Milliseconds 500
        }
    }
}
finally {
    if ($backup) { Set-Content $cfg $backup -Encoding utf8 -NoNewline } else { Remove-Item $cfg -ErrorAction SilentlyContinue }
    Remove-Item Env:CLAUDEWATCH_SHOWROOM -ErrorAction SilentlyContinue
}

if ($Assets) {
    $destino = Join-Path $PSScriptRoot '..\assets'
    foreach ($estilo in $Estilos) {
        $arq = Join-Path $Saida "ok-$($estilo.ToLower()).png"
        if (Test-Path $arq) {
            Copy-Item $arq (Join-Path $destino "$($estilo.ToLower()).png") -Force
            Write-Host "assets\$($estilo.ToLower()).png atualizado"
        } else {
            Write-Warning "ok-$($estilo.ToLower()).png não foi capturado; assets\ mantém o print anterior"
        }
    }
}
