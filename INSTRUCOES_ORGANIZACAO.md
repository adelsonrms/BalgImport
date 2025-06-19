# Instruções para Organização dos Arquivos Agrupados

Este projeto foi agrupado em três arquivos principais para facilitar a cópia e reconstrução em outro local:

- `fonte_csharp.cs`: Contém todo o código C# do projeto (Program, Models, Hubs, Services, Controllers).
- `fonte_js.js`: Contém todos os arquivos JavaScript próprios do projeto.
- `fonte_views.cshtml`: Contém todas as views .cshtml do projeto.

## Como reconstruir o projeto

1. **Crie a estrutura de pastas** conforme o projeto original:
   - `Controllers/`
   - `Hubs/`
   - `Models/`
   - `Services/`
   - `Views/` (e subpastas)
   - `wwwroot/js/`

2. **Separe os arquivos**:
   - Copie cada bloco de código do arquivo `fonte_csharp.cs` para o respectivo arquivo original, conforme o comentário de separação (exemplo: Models/UploadBatch.cs, Services/ImportacaoService.cs, etc).
   - Faça o mesmo para os arquivos JavaScript em `fonte_js.js` (exemplo: wwwroot/js/testData.js, wwwroot/js/DevonnoUploader.js, etc).
   - Para as views, copie cada bloco do arquivo `fonte_views.cshtml` para o respectivo arquivo em `Views/` e subpastas.

3. **Ajuste os usings**:
   - Os usings globais estão no topo do `fonte_csharp.cs`. Mantenha apenas os necessários em cada arquivo ao separar.

4. **Restaure os arquivos de configuração**:
   - Não esqueça de copiar também os arquivos de configuração (`appsettings.json`, `appsettings.Development.json`, `Web.config`, etc) e dependências (`BalgImport.csproj`, `packages.config`).

5. **Dependências**:
   - Certifique-se de instalar os pacotes NuGet necessários, conforme listado no `.csproj`.

6. **Testes**:
   - Após separar os arquivos, abra o projeto no Visual Studio ou VS Code, restaure os pacotes e faça um build para garantir que tudo está correto.

---

**Dica:**
- Utilize um editor de texto com suporte a múltiplos cursores ou busca por comentários para facilitar a separação dos blocos.
- Se necessário, consulte o repositório original para conferir a estrutura de pastas e arquivos. 