param(
    [string]$NomeProjeto = "BalgImportNovo",
    [string]$Destino = "./NovoProjeto"
)

# 1. Criar diretório de destino
if (!(Test-Path $Destino)) {
    New-Item -ItemType Directory -Path $Destino | Out-Null
}
Set-Location $Destino

# 2. Criar projeto ASP.NET Core MVC limpo
Write-Host "Criando projeto ASP.NET Core MVC..."
dotnet new mvc -n $NomeProjeto --no-https
Set-Location $NomeProjeto

# 3. Adicionar referências NuGet necessárias
Write-Host "Adicionando pacotes NuGet..."
dotnet add package CsvHelper --version 30.0.1
dotnet add package Microsoft.AspNet.SignalR.Core --version 2.4.3
dotnet add package Microsoft.AspNet.SignalR.SystemWeb --version 2.4.3
dotnet add package Microsoft.AspNetCore.SignalR.Client --version 6.0.27
dotnet add package Microsoft.Data.SqlClient --version 5.1.3
dotnet add package Microsoft.Owin --version 4.2.2
dotnet add package Microsoft.Owin.Host.SystemWeb --version 4.2.2
dotnet add package Microsoft.Owin.Security --version 4.2.2
dotnet add package Newtonsoft.Json --version 13.0.3
dotnet add package Owin --version 1.0.0
dotnet add package Swashbuckle.AspNetCore --version 6.4.0

# 4. Copiar arquivos agrupados para a raiz do projeto
$origem = $PSScriptRoot
foreach ($arq in 'fonte_csharp.cs','fonte_js.js','fonte_views.cshtml') {
    if (Test-Path (Join-Path $origem $arq)) {
        Copy-Item (Join-Path $origem $arq) .
    } else {
        Write-Host "Arquivo $arq não encontrado em $origem. Abortando." -ForegroundColor Red
        exit 1
    }
}

# 5. Executar desagrupamento
Write-Host "Desagrupando arquivos..."
$desagrupar = Join-Path $origem 'desagrupar_projeto.ps1'
if (Test-Path $desagrupar) {
    & $desagrupar
} else {
    Write-Host "Script desagrupar_projeto.ps1 não encontrado em $origem. Abortando." -ForegroundColor Red
    exit 1
}

# 6. Build do projeto
Write-Host "Executando build do projeto..."
dotnet build

Write-Host "\nProjeto importado, desagrupado e pronto para uso!"
Write-Host "Abra a solução em $Destino\$NomeProjeto e execute normalmente." 