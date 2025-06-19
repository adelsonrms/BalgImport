# Script PowerShell para desagrupar os arquivos fonte_csharp.cs, fonte_js.js e fonte_views.cshtml
# e restaurar a estrutura de diretórios e arquivos do projeto

function Separar-Blocos {
    param(
        [string]$ArquivoFonte,
        [string]$PadraoSeparador,
        [string]$DiretorioBase,
        [string]$PrefixoRemover = ''
    )
    $conteudo = Get-Content $ArquivoFonte -Raw
    $blocos = [regex]::Split($conteudo, $PadraoSeparador)
    $matches = [regex]::Matches($conteudo, $PadraoSeparador)
    for ($i = 1; $i -lt $blocos.Count; $i++) {
        $match = $matches[$i-1].Value
        $relPath = $match -replace '[^=]+= ', '' -replace ' =+.*', '' -replace '\*', '' -replace '@', '' -replace '\s', ''
        $relPath = $relPath.Trim()
        if ($PrefixoRemover -and $relPath.StartsWith($PrefixoRemover)) {
            $relPath = $relPath.Substring($PrefixoRemover.Length)
        }
        $relPath = $relPath.TrimStart('/','\\')
        $destino = Join-Path $DiretorioBase $relPath
        $dir = Split-Path $destino -Parent
        if (!(Test-Path $dir)) { New-Item -ItemType Directory -Path $dir | Out-Null }
        $bloco = $blocos[$i].Trim()
        if ($bloco) { Set-Content -Path $destino -Value $bloco -Encoding UTF8 }
    }
}

# Desagrupar C#
if (Test-Path 'fonte_csharp.cs') {
    Write-Host 'Desagrupando fonte_csharp.cs...'
    Separar-Blocos -ArquivoFonte 'fonte_csharp.cs' -PadraoSeparador '(?m)^// =+ ([^=]+) =+\n' -DiretorioBase '.'
}

# Desagrupar JS
if (Test-Path 'fonte_js.js') {
    Write-Host 'Desagrupando fonte_js.js...'
    Separar-Blocos -ArquivoFonte 'fonte_js.js' -PadraoSeparador '(?m)^// =+ ([^=]+) =+\n' -DiretorioBase '.'
}

# Desagrupar Views
if (Test-Path 'fonte_views.cshtml') {
    Write-Host 'Desagrupando fonte_views.cshtml...'
    Separar-Blocos -ArquivoFonte 'fonte_views.cshtml' -PadraoSeparador '(?m)^@\* =+ ([^=]+) =+ \*@\n' -DiretorioBase '.'
}

Write-Host 'Desagrupamento concluído!' 