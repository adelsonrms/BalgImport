# Cria a pasta se não existir
$destino = "C:\temp\EP_Gabriela"
if (!(Test-Path -Path $destino)) {
    New-Item -ItemType Directory -Path $destino
}

# Loop para os arquivos de 065 até 080
for ($i = 65; $i -le 80; $i++) {
    # Formata o número com zero à esquerda
    $numero = "{0:D3}" -f $i
    
    # Monta a URL
    $url = "https://cdn-novflix.com/storage7/GAB/GAB-$numero.mp4"
    
    # Define o caminho de destino
    $arquivo = Join-Path -Path $destino -ChildPath "GAB-$numero.mp4"
    
    Write-Host "Baixando $url para $arquivo ..."
    
    try {
        Invoke-WebRequest -Uri $url -OutFile $arquivo
        Write-Host "Download de GAB-$numero.mp4 concluído." -ForegroundColor Green
    }
    catch {
        Write-Host "Falha ao baixar GAB-$numero.mp4" -ForegroundColor Red
    }
}

Write-Host "Todos os downloads foram finalizados." -ForegroundColor Cyan
