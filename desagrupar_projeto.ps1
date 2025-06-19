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
    # Encontra todos os separadores e extrai os nomes dos arquivos
    $matches = [regex]::Matches($conteudo, $PadraoSeparador)
    # Separa o conteúdo em blocos (cada bloco corresponde a um arquivo)
    $blocos = [regex]::Split($conteudo, $PadraoSeparador)
    # O primeiro bloco ($blocos[0]) é o conteúdo antes do primeiro separador (normalmente vazio)
    if ($matches.Count -eq 0) {
        Write-Host "Nenhum separador encontrado em $ArquivoFonte. Verifique o padrão!" -ForegroundColor Yellow
        return
    }
    for ($i = 1; $i -lt $blocos.Count; $i++) {
        $relPath = $matches[$i-1].Groups[1].Value.Trim()
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

# Padrões ajustados para maior robustez
$PadraoSeparadorCS = '(?m)^// =+ ([^=]+) =+.*$'
$PadraoSeparadorJS = $PadraoSeparadorCS
$PadraoSeparadorViews = '(?m)^@\* =+ ([^=]+) =+ \*@.*$'

# Desagrupar C#
if (Test-Path 'fonte_csharp.cs') {
    Write-Host 'Desagrupando fonte_csharp.cs...'
    Separar-Blocos -ArquivoFonte 'fonte_csharp.cs' -PadraoSeparador $PadraoSeparadorCS -DiretorioBase '.'
}

# Desagrupar JS
if (Test-Path 'fonte_js.js') {
    Write-Host 'Desagrupando fonte_js.js...'
    Separar-Blocos -ArquivoFonte 'fonte_js.js' -PadraoSeparador $PadraoSeparadorJS -DiretorioBase '.'
}

# Desagrupar Views
if (Test-Path 'fonte_views.cshtml') {
    Write-Host 'Desagrupando fonte_views.cshtml...'
    Separar-Blocos -ArquivoFonte 'fonte_views.cshtml' -PadraoSeparador $PadraoSeparadorViews -DiretorioBase '.'
}

Write-Host 'Desagrupamento concluído!' 