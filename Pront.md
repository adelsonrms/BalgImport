Uploder Orchestrater

Crie uma aplicação ASPNET CORE NET6  MVC que terá como objetivo central as seguintes 
 - Orquestração rubosta e inteligente de uploads de varios arquivos simultaneamente para o servidor em uma pasta de destino.

 Funcionalides esperadas :
  * Permitir selecionar 1 ou varios arquivos para seren enviados.
  * Cada deselção deve tratada com um lote (batch) de arquivos
  * cada arquivo deve ser enviado assyncronamente e deve ser controlado sua assyncronidade tanto no backend quando no front
  * Cada envio deve ser criado uma especie de "monitor" que ficará "escutando" o status do envio do arquivo que por sua vez deve atualizar o status na tela.
  * ao final de cada envio, deve ser sinalizado do "monitor do lote" que o arquivo foi processado com sucesso ou com erro.
  * Se ocorrer erros, o sistema deve captar informações detalhadas do erro para devida identificação e correção.
  * Deverá ser permitido o envio de varios lotes simultaneamente

# Fron - End 

ASPNET MVC com Razor

A aplicação deverá ter apenas uma Tela "Uploader" no qual devemos ter um quadro de seleção de multiplus arquivos.
Logo abaixo um container que dividira a tela em duas partes :
1 - Lista dos lotes que foram invocados - 35% da container
2 - Lista dos arquivos vinculados ao lote selecionado

# Regras 

## Para cada LOTE devemos ter :

1. ID unico do lote de envio
2. Quantidade de arquivos
3. Status de processamento (Progress com % e (1 de 6))
4. Data de Inicio e Fim

## Para cada ARQUIVO devemos ter :

1. ID unico do prossamento do upload
2. Status (Enviando, processando, concluido)
3. Data de Inicio e Fim
4. Ações (Visualizar o arquivo em tela com formato de tabela ou texto puro csv)

# BACKEND

O backe end deve ser criado em aspnet com os modelos identificado acima
Nao havera persistencia em banco
Respeitar e controlar todas as tecnicas de Assyncronidade
