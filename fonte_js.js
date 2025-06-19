// ========== wwwroot\js\BatchCardsRenderer.js ==========


// ========== wwwroot\js\DevonnoUploader.js ==========
﻿// Constantes e configurações
const STATUS_CONFIG = {
    CONCLUIDO: {
        text: 'Concluído',
        class: 'bg-success'
    },
    PROCESSANDO: {
        text: 'Processando',
        class: 'bg-warning'
    },
    ERRO: {
        text: 'Erro',
        class: 'bg-danger'
    },
    PENDENTE: {
        text: 'Pendente',
        class: 'bg-secondary'
    },
    CANCELADO: {
        text: 'Cancelado',
        class: 'bg-dark'
    },
    UPLOADING: {
        text: 'Enviando',
        class: 'bg-info'
    }
};

// Interface do objeto de status
const StatusImportacao = {
    idUpload: null,          // ID do upload
    idLote: null,           // ID do lote
    idArquivo: null,        // ID do arquivo
    nomeArquivo: null,      // Nome do arquivo
    etapa: null,            // Etapa atual (INICIO, PROCESSANDO, etc)
    status: null,           // Status atual (INICIADO, PROCESSANDO, etc)
    mensagem: null,         // Mensagem descritiva
    dataHora: null,         // Data e hora do status
    totalArquivos: 0,       // Total de arquivos no lote
    arquivosProcessados: 0  // Quantidade de arquivos processados
};

// Estado da aplicação
const appState = {
    selectedBatchId: null,
    hasProcessingBatches: false,
    connection: null,
    batchErrors: {}, // Armazena erros por idLote
    lotes: {}, // Armazena lotes processados
    lotes_uploads: [] // Armazena lotes processados
};

// Elementos do DOM
const uploadForm = document.getElementById('uploadForm');
const fileInput = document.getElementById('files');
const batchTableBody = document.getElementById('batchTableBody');
const fileTableBody = document.getElementById('fileTableBody');
const errorModal = document.getElementById('errorModal');
const errorMessage = document.getElementById('errorMessage');

// Inicialização
document.addEventListener('DOMContentLoaded', () => {
    if (uploadForm) {
        uploadForm.addEventListener('submit', handleUpload);
    }
    if (fileInput) {
        fileInput.addEventListener('change', updateSelectedFiles);
    }

    // Inicializa a conexão SignalR
    initSignalR();
});

// Função para gerar ID único via backend
async function generateUniqueIdFromServer() {
    try {
        const response = await fetch('/api/ImportacaoApi/gerar-id-unico', {
            method: 'GET',
            headers: {
                'Content-Type': 'application/json'
            }
        });

        if (!response.ok) {
            throw new Error('Erro ao gerar ID único');
        }

        const result = await response.json();
        if (!result.success) {
            throw new Error(result.erro || 'Erro ao gerar ID único');
        }

        // Garante que o ID seja um número inteiro
        const idUnico = parseInt(result.idUnico);
        console.log(`🔑 ID único gerado pelo servidor: ${idUnico}`);
        return idUnico;
    } catch (error) {
        console.error('Erro ao gerar ID único:', error);
        throw error;
    }
}

// Função para lidar com o upload
let currentBatchId = null;
let batchCounter = 1;

async function handleUpload() {
    if (window.BatchCardsRenderer) {
        window.BatchCardsRenderer.resetarCards();
    }
    const fileInput = document.getElementById('files');
    const files = Array.from(fileInput.files);

    if (!files.length) {
        showError('Selecione pelo menos um arquivo.');
        return;
    }

    try {
        // Gera um ID único para todo o upload via backend
        const idUpload = await generateUniqueIdFromServer();
        console.log(`🚀 Iniciando upload com ID: ${idUpload}`);
        console.log(`📦 Total de arquivos: ${files.length}`);
        appState.lotes = {};
        appState.lotes_uploads = [];
        
        const batchSize = 5;// Math.ceil(files.length / 10); // Quantidade de arquivos por lote no front
        const totalBatches = Math.ceil(files.length / batchSize);

        console.log(`🗂️ Listando os status atuais em ${totalBatches} lotes de até ${batchSize} arquivos.`);

        // Cria os lotes iniciais
        const lotes = [];

        for (let i = 0; i < totalBatches; i++) {
            const start = i * batchSize;
            const end = start + batchSize;
            const arquivosLote = files.slice(start, end);
            
            // Gera ID único para cada lote via backend
            const idLote = await generateUniqueIdFromServer();

            // Calcula o tamanho total dos arquivos do lote
            const tamanhoTotalBytes = arquivosLote.reduce((total, file) => total + file.size, 0);

            // Armazena informações do lote
            lotes.push({
                id: idLote,
                numero: i + 1,
                arquivos: arquivosLote,
                totalArquivos: arquivosLote.length,
                arquivosProcessados: 0,
                tamanhoTotalBytes: tamanhoTotalBytes
            });

            var data = {
                idUpload: idUpload,          // ID do upload
                idLote: idLote,           // ID do lote
                idArquivo: null,        // ID do arquivo
                nomeArquivo: null,      // Nome do arquivo
                etapa: 'INICIO',            // Etapa atual (INICIO, PROCESSANDO, etc)
                status: 'AGUARDANDO',           // Status atual (INICIADO, PROCESSANDO, etc)
                mensagem: '',         // Mensagem descritiva
                dataHora: new Date().toISOString(),         // Data e hora do status
                totalArquivos: arquivosLote.length,       // Total de arquivos no lote
                arquivosProcessados: 0,  // Quantidade de arquivos processados
                tamanhoTotalBytes: tamanhoTotalBytes  // Tamanho total dos arquivos em bytes
            }

            processarAtualizacaoStatus(data);
        }


        // 2. Inicia o processamento dos lotes
        console.log(`🚀 Iniciando processamento dos ${totalBatches} lotes`);

        for (const lote of lotes) {
            try {
                appState.lotes_uploads.push(lote);

                // Atualiza status para PROCESSANDO
                updateBatchStatus(lote.id, 0, lote.totalArquivos, 'PROCESSANDO');
                
                // Envia o lote para processamento
                await uploadBatch(lote.arquivos, lote.numero, totalBatches, idUpload, lote.id);
                
                //// Inicia o monitoramento do lote
                //startProcessingMonitor(lote.id);
                
            } catch (error) {
                console.error(`❌ Erro no lote ${lote.numero}:`, error);
                updateBatchStatus(lote.id, lote.arquivosProcessados, lote.totalArquivos, 'ERRO');
                showError(`Erro no lote ${lote.numero}: ${error.message}`);
                // Continua com o próximo lote ao invés de interromper todo o processo
            }
        }

        fileInput.value = '';
    } catch (error) {
        console.error('Erro ao gerar IDs únicos:', error);
        showError(`Erro ao iniciar upload: ${error.message}`);

    }
}

async function uploadBatch(batch, batchNumber, totalBatches, idUpload, idLote) {

    const formData = new FormData();
    
    // Adiciona os arquivos ao FormData
    batch.forEach(file => {
        formData.append('arquivos', file);
    });
    
    // Adiciona os IDs ao FormData
    formData.append('idUpload', idUpload);
    formData.append('idLote', idLote);
    formData.append('usuario', document.getElementById('usuario').value);

    const response = await fetch('/api/ImportacaoApi/upload', {
        method: 'POST',
        body: formData
    });

    if (!response.ok) {
        const error = await response.json();
        throw new Error(error.erro || `Erro no lote ${batchNumber}`);
    }

    const result = await response.json();
    console.log(`✅ Lote ${batchNumber}/${totalBatches} enviado com sucesso:`, result);
}

// Funções de manipulação de eventos
function updateSelectedFiles() {
    if (fileInput && fileInput.files.length > 0) {
        const files = Array.from(fileInput.files);
        const totalFiles = files.length;
        const displayCount = Math.min(4, totalFiles);
        
        const fileList = files
            .slice(0, displayCount)
            .map(file => file.name)
            .join(', ');
            
        const remainingCount = totalFiles - displayCount;
        console.log(remainingCount > 0 
            ? `${fileList}... mais ${remainingCount} arquivos de um total de ${totalFiles}`
            : fileList);
    }
}

function addBatchToTable(batchId, totalFiles) {
    const tbody = document.getElementById('batchTableBody');
    const row = document.createElement('tr');
    row.id = `batch-${batchId}`;
    row.innerHTML = `
        <td>#${batchCounter++}</td>
        <td>0 / ${totalFiles}</td>
        <td>${new Date().toLocaleString()}</td>
        <td><span class="badge bg-warning">Em andamento</span></td>
    `;
    tbody.insertBefore(row, tbody.firstChild);
}

function updateBatchStatus(batchId, processedFiles, totalFiles, status) {
    
    let row = document.querySelector(`#batchTableBody tr[data-batch-id="${batchId}"]`);

    if (row) {
        const statusBadge = row.querySelector('.badge');
        const statusText = getStatusText(status);
        const statusClass = getStatusClass(status);
        
        row.children[2].textContent = `${processedFiles} / ${totalFiles}`;

        statusBadge.className = `badge ${statusClass}`;
        statusBadge.textContent = statusText;
    }
}

function addFileToTable(fileName, status = 'Uploading') {
    const tbody = document.getElementById('fileTableBody');
    const row = document.createElement('tr');
    row.id = `file-${fileName}`;
    
    const [empresa, layout] = fileName.split('_');
    
    row.innerHTML = `
        <td>${fileName}</td>
        <td>${empresa || '-'}</td>
        <td>${layout || '-'}</td>
        <td><span class="badge ${getStatusClass(status)}">${getStatusText(status)}</span></td>
        <td>${new Date().toLocaleString()}</td>
        <td>-</td>
    `;
    tbody.insertBefore(row, tbody.firstChild);
}

function updateFileStatus(fileName, status, error = null) {
    const row = document.getElementById(`file-${fileName}`);
    if (row) {
        const statusBadge = row.querySelector('.badge');
        const statusText = getStatusText(status);
        const statusClass = getStatusClass(status);
        
        statusBadge.className = `badge ${statusClass}`;
        statusBadge.textContent = statusText;
        
        if (status === 'CONCLUIDO' || status === 'ERRO') {
            row.children[5].textContent = new Date().toLocaleString();
        }
        
        if (error) {
            statusBadge.title = error;
        }
    }
}

function getStatusText(status) {
    if (!status) return 'Desconhecido';
    return STATUS_CONFIG[status.toUpperCase()]?.text || status;
}

function getStatusClass(status) {
    if (!status) return 'bg-secondary';
    return STATUS_CONFIG[status.toUpperCase()]?.class || 'bg-secondary';
}

// Função utilitária para formatar duração por extenso
function formatDuration(start, end) {
    if (!start || !end) return '-';
    const ms = Math.abs(new Date(end) - new Date(start));
    if (isNaN(ms)) return '-';
    const s = Math.floor(ms / 1000);
    const min = Math.floor(s / 60);
    const sec = s % 60;
    if (min > 0) return `${min} min${min > 1 ? 's' : ''} e ${sec}s`;
    return `${sec}s`;
}

// Atualiza o estado global de lotes e renderiza os cards
function processarAtualizacaoStatus(status) {
    try {
        if (!status || !status.idLote) {
            console.error('Status inválido:', status);
            return;
        }
        // Inicializa o objeto global de lotes se necessário
        if (!appState.lotes) appState.lotes = {};
        // Atualiza ou cria o lote
        const lote = appState.lotes[status.idLote] || {};
        // Copia todos os campos relevantes do status para o lote
        Object.assign(lote, status);
        // Garante campos obrigatórios
        lote.idLote = status.idLote;
        lote.idUpload = status.idUpload;
        lote.status = status.status;
        lote.mensagem = status.mensagem;
        lote.arquivosProcessados = status.arquivosProcessados;
        lote.totalArquivos = status.totalArquivos;
        lote.dataInicio = status.dataInicio || status.dataHora || null;
        lote.dataFim = status.dataFim || null;
        lote.usuario = status.usuario || '-';
        // Armazena erros do lote, se houver
        if (status.status && status.status.toUpperCase() === 'ERRO' && status.mensagem) {
            if (!appState.batchErrors[status.idLote]) appState.batchErrors[status.idLote] = [];
            appState.batchErrors[status.idLote].push(status.mensagem);
        }
        appState.lotes[status.idLote] = lote;
        if (window.BatchCardsRenderer) {
            window.BatchCardsRenderer.atualizarTodosCards(Object.values(appState.lotes), lote);
        }
        // Atualização de arquivos individuais permanece igual
        if (status.idArquivo && status.nomeArquivo) {
            const fileRow = document.querySelector(`#fileTableBody tr[data-file-id="${status.idArquivo}"]`);
            if (fileRow) {
                const statusBadge = fileRow.querySelector('.badge');
                statusBadge.className = `badge ${getStatusClass(status.status)}`;
                statusBadge.title = status.mensagem || '';
                statusBadge.textContent = getStatusText(status.status);
            }
        }
        console.log('Status atualizado com sucesso:', status);
    } catch (error) {
        console.error('Erro ao processar atualização de status:', error);
    }
}

// Funções de atualização da UI
let pollingInterval = null;

function startPolling() {
    if (pollingInterval) {
        clearInterval(pollingInterval);
    }

    pollingInterval = setInterval(updateInterface, 2000);
}

function updateInterface() {
    fetch('/Upload/TodosBatches')
        .then(response => response.json())
        .then(data => {
            if (data.success) {
                // Atualiza a tabela de lotes
                batchTableBody.innerHTML = '';
                let batchCounter = 1;

                data.batches.forEach(batch => {
                    const row = document.createElement('tr');
                    row.setAttribute('data-batch-id', batch.id);
                    
                    // Calcula o status do lote com base nos arquivos
                    const batchStatus = getBatchStatus(batch);
                    const statusClass = getStatusClass(batchStatus);
                    const statusText = getStatusText(batchStatus);
                    
                    // Adiciona classe ativa se for o lote selecionado
                    if (appState.selectedBatchId === batch.id) {
                        row.classList.add('table-active');
                    }
                    
                    // Adiciona evento de clique para selecionar o lote
                    row.onclick = () => selectBatch(batch.id);
                    
                    row.innerHTML = `
                        <td>#${batchCounter++}</td>
                        <td>${batch.arquivosProcessados} / ${batch.totalArquivos}</td>
                        <td>${formatDate(batch.dataInicio)}</td>
                        <td><span class="badge ${statusClass}">${statusText}</span></td>
                    `;

                    batchTableBody.appendChild(row);
                });

                // Se houver um batch selecionado, atualiza a tabela de arquivos
                if (appState.selectedBatchId) {
                    const selectedBatch = data.batches.find(b => b.id === appState.selectedBatchId);
                    if (selectedBatch) {
                        updateFileTable(selectedBatch);
                    }
                }
            } else {
                showError(data.error || 'Erro ao carregar batches');
            }
        })
        .catch(error => {
            console.error('Erro ao atualizar interface:', error);
            showError('Erro ao carregar dados');
        });
}

function updateFileTable(batch) {
    fileTableBody.innerHTML = '';
    
    batch.arquivos.forEach(arquivo => {
        const row = document.createElement('tr');
        const statusClass = getStatusClass(arquivo.status);
        const statusText = getStatusText(arquivo.status);

        const [empresa, layout] = arquivo.nomeArquivo.split('_');

        row.innerHTML = `
            <td>${arquivo.nomeArquivo}</td>
            <td>${empresa || '-'}</td>
            <td>${layout || '-'}</td>
            <td><span class="badge ${statusClass}">${statusText}</span></td>
            <td>${formatDate(arquivo.dataInicio)}</td>
            <td>${arquivo.dataFim ? formatDate(arquivo.dataFim) : '-'}</td>
        `;

        if (arquivo.mensagemErro) {
            row.querySelector('.badge').title = arquivo.mensagemErro;
        }

        fileTableBody.appendChild(row);
    });
}

function formatDate(dateString) {
    if (!dateString) return '-';
    const date = new Date(dateString);
    return date.toLocaleTimeString();
}

function selectBatch(batchId) {
    if (!batchId) {
        console.error('BatchId inválido');
        return;
    }

    // Remove a classe ativa de todas as linhas
    document.querySelectorAll('#batchTableBody tr').forEach(row => {
        row.classList.remove('table-active');
    });

    // Adiciona a classe ativa na linha selecionada
    const selectedRow = document.querySelector(`#batchTableBody tr[data-batch-id="${batchId}"]`);
    if (selectedRow) {
        selectedRow.classList.add('table-active');
    }

    // Atualiza o ID do lote selecionado
    appState.selectedBatchId = batchId;

    // Busca os detalhes do lote
    fetch(`/Upload/ObterStatusBatch/${batchId}`)
        .then(response => response.json())
        .then(data => {
            if (data.success) {
                updateFileTableFromData(data.batch);
            } else {
                showError(data.error || 'Erro ao carregar detalhes do lote');
            }
        })
        .catch(error => {
            console.error('Erro ao carregar detalhes do lote:', error);
            showError('Erro ao carregar detalhes do lote');
        });
}

function formatFileSize(bytes) {
    if (bytes === 0) return '0 Bytes';
    const k = 1024;
    const sizes = ['Bytes', 'KB', 'MB', 'GB'];
    const i = Math.floor(Math.log(bytes) / Math.log(k));
    return parseFloat((bytes / Math.pow(k, i)).toFixed(2)) + ' ' + sizes[i];
}

function showError(message) {
    console.error('Exibindo erro:', message);
    const errorModal = new bootstrap.Modal(document.getElementById('errorModal'));
    document.getElementById('errorMessage').textContent = message;
    errorModal.show();
}


async function retomarUpload(batchId, fileName) {
    try {
        const response = await fetch('/upload/retomarupload', {
            method: 'POST',
            headers: {
                'Content-Type': 'application/json'
            },
            body: JSON.stringify({ batchId, fileName })
        });

        if (!response.ok) {
            throw new Error('Erro ao retomar upload');
        }

        const result = await response.json();
        if (!result.success) {
            throw new Error(result.error || 'Erro ao retomar upload');
        }

        await updateInterface();
    } catch (error) {
        console.error('Erro ao retomar upload:', error);
        showError('Erro', error.message);
    }
}

function updateBatchTable(batches) {
    batchTableBody.innerHTML = '';
    batchCounter = 1;

    batches.forEach(batch => {
        const row = document.createElement('tr');
        const statusClass = getStatusClass(batch.status);
        const statusText = getStatusText(batch.status);
        
        row.innerHTML = `
            <td>#${batchCounter++}</td>
            <td>${batch.arquivosProcessados} / ${batch.totalArquivos}</td>
            <td>${formatDate(batch.dataInicio)}</td>
            <td><span class="badge ${statusClass}">${statusText}</span></td>
        `;

        batchTableBody.appendChild(row);
    });
}

function getBatchStatus(batch) {
    if (!batch.arquivos || batch.arquivos.length === 0) {
        return 'PENDENTE';
    }

    const statusCount = batch.arquivos.reduce((acc, arquivo) => {
        acc[arquivo.status] = (acc[arquivo.status] || 0) + 1;
        return acc;
    }, {});

    if (statusCount['CONCLUIDO'] === batch.arquivos.length) return 'CONCLUIDO';
    if (statusCount['ERRO'] === batch.arquivos.length) return 'ERRO';
    if (statusCount['PROCESSANDO'] > 0) return 'PROCESSANDO';
    if (statusCount['PENDENTE'] > 0) return 'PENDENTE';
    if (statusCount['ERRO'] > 0) return 'ERRO';

    return 'PENDENTE';
}

// Inicializa a conexão SignalR
function initSignalR() {

    console.log('Inicializando conexão SignalR...');
    
    appState.connection = new signalR.HubConnectionBuilder()
        .withUrl("/importacaoHub")
        .withAutomaticReconnect([0, 2000, 5000, 10000, 20000]) // Tenta reconectar em intervalos crescentes
        .configureLogging(signalR.LogLevel.Debug) // Habilita logs detalhados
        .build();

    // Configura handlers de reconexão
    appState.connection.onreconnecting((error) => {
        console.log('SignalR: Reconectando...', error);
    });

    appState.connection.onreconnected((connectionId) => {
        console.log('SignalR: Reconectado com ID:', connectionId);
    });

    appState.connection.onclose((error) => {
        console.log('SignalR: Conexão fechada', error);
    });

    // Configura o handler de status
    appState.connection.on("ReceberStatusImportacao", (data) => {
        console.log('SignalR: Status recebido:', data);
        processarAtualizacaoStatus(data);
    });

    // Inicia a conexão
    appState.connection.start()
        .then(() => {
            console.log('SignalR: Conexão estabelecida com sucesso');
            // Carrega os dados iniciais apenas uma vez
            //loadInitialData();
        })
        .catch(error => {
            console.error('SignalR: Erro ao conectar:', error);
            showError('Erro ao conectar ao servidor');
        });
}

function loadInitialData() {
    fetch('/Upload/TodosBatches')
        .then(response => response.json())
        .then(data => {
            if (data.success) {
                // Atualiza a tabela de lotes
                batchTableBody.innerHTML = '';
                let batchCounter = 1;

                data.batches.forEach(batch => {
                    const row = document.createElement('tr');
                    row.setAttribute('data-batch-id', batch.id);
                    
                    const statusClass = getStatusClass(batch.status);
                    const statusText = getStatusText(batch.status);
                    
                    row.innerHTML = `
                        <td>#${batchCounter++}</td>
                        <td>${batch.arquivosProcessados} / ${batch.totalArquivos}</td>
                        <td>${formatDate(batch.dataInicio)}</td>
                        <td><span class="badge ${statusClass}">${statusText}</span></td>
                    `;
                    
                    row.onclick = () => selectBatch(batch.id);
                    batchTableBody.appendChild(row);
                });
            } else {
                showError(data.error || 'Erro ao carregar batches');
            }
        })
        .catch(error => {
            console.error('Erro ao carregar dados iniciais:', error);
            showError('Erro ao carregar dados iniciais');
        });
}

function updateFileTableFromData(data) {
    fileTableBody.innerHTML = '';

    data.arquivos.forEach(arquivo => {
        const row = document.createElement('tr');
        const statusClass = getStatusClass(arquivo.status);
        const statusText = getStatusText(arquivo.status);
        
        // Extrai empresa e layout do nome do arquivo
        const [empresa, layout] = arquivo.nomeArquivo.split('_');

        row.innerHTML = `
            <td>${arquivo.nomeArquivo}</td>
            <td>${empresa || '-'}</td>
            <td>${layout || '-'}</td>
            <td><span class="badge ${statusClass}" title="${arquivo.mensagemErro || ''}">${statusText}</span></td>
            <td>${formatDate(arquivo.dataInicio)}</td>
            <td>${arquivo.dataFim ? formatDate(arquivo.dataFim) : '-'}</td>
        `;

        fileTableBody.appendChild(row);
    });
}

// Atualiza a função de monitoramento para usar a nova função
async function startProcessingMonitor(batchId) {
    if (!batchId) {
        console.error('ID do lote inválido');
        return;
    }

    console.log(`Iniciando monitoramento do batch: ${batchId}`);

    const checkStatus = async () => {
        try {
            console.log(`Verificando status do batch: ${batchId}`);
            const response = await fetch(`/Upload/Status/${batchId}`);
            
            if (!response.ok) {
                const errorText = await response.text();
                console.error(`Erro na resposta do servidor: ${errorText}`);
                throw new Error('Erro ao verificar status');
            }

            const result = await response.json();
            if (!result.success) {
                console.error(`Erro retornado pelo servidor: ${result.error}`);
                throw new Error(result.error || 'Erro ao verificar status');
            }

            const batch = result.batch;
            if (!batch) {
                console.error('Dados do lote não retornados pelo servidor');
                throw new Error('Dados do lote não retornados pelo servidor');
            }
            
            processarAtualizacaoStatus(batch);

            // Se o lote ainda não foi concluído, continua monitorando
            if (batch.status !== 'CONCLUIDO' && batch.status !== 'CANCELADO') {
                setTimeout(checkStatus, 1000);
            }
        } catch (error) {
            console.error('Erro ao monitorar processamento:', error);
            setTimeout(checkStatus, 5000); // Tenta novamente em 5 segundos
        }
    };

    checkStatus();
}

// Função para exibir a modal de erros do lote
function showBatchErrors(idLote) {
    const errors = appState.batchErrors[idLote] || [];
    const contentDiv = document.getElementById('batchErrorsContent');
    if (contentDiv) {
        if (errors.length === 0) {
            contentDiv.innerHTML = '<div class="alert alert-success">Nenhum erro registrado para este lote.</div>';
        } else {
            contentDiv.innerHTML = '<ul class="list-group">' +
                errors.map(e => `<li class="list-group-item text-danger"><pre style="white-space: pre-wrap;">${e}</pre></li>`).join('') +
                '</ul>';
        }
    }
    const modal = new bootstrap.Modal(document.getElementById('batchErrorsModal'));
    modal.show();
}



// BatchCardsRenderer.js
// Responsável por atualizar os cards de Resumo, Lote Atual e Histórico na tela de upload

function atualizarCardResumo(lotes) {
    const resumoIdUpload = document.getElementById('resumo-id-upload');
    const resumoDataInicio = document.getElementById('resumo-data-inicio');
    const resumoQtdLotes = document.getElementById('resumo-qtd-lotes');
    const resumoLoteProgresso = document.getElementById('resumo-lote-progresso');
    const resumoLoteProgressbar = document.getElementById('resumo-lote-progressbar');
    const resumoHoraInicio = document.getElementById('resumo-hora-inicio');
    const resumoHoraFim = document.getElementById('resumo-hora-fim');
    const resumoDuracao = document.getElementById('resumo-duracao');

    if (!lotes || lotes.length === 0) {
        if (resumoIdUpload) resumoIdUpload.textContent = '-';
        if (resumoDataInicio) resumoDataInicio.textContent = '-';
        if (resumoQtdLotes) resumoQtdLotes.textContent = '-';
        if (resumoLoteProgresso) resumoLoteProgresso.textContent = '-';
        if (resumoLoteProgressbar) resumoLoteProgressbar.style.width = '0%';
        if (resumoHoraInicio) resumoHoraInicio.textContent = '-';
        if (resumoHoraFim) resumoHoraFim.textContent = '-';
        if (resumoDuracao) resumoDuracao.textContent = '-';
        return;
    }
    lotes.sort((a, b) => new Date(a.dataInicio) - new Date(b.dataInicio));
    const idUpload = lotes[0].idUpload || '-';
    const dataInicio = lotes[0].dataInicio ? formatDateFull(lotes[0].dataInicio) : '-';
    const qtdLotes = lotes.length;
    const lotesFinalizados = lotes.filter(l => l.status && (l.status.toUpperCase() === 'CONCLUIDO' || l.status.toUpperCase() === 'ERRO'));
    const loteAtual = lotes.find(l => l.status && l.status.toUpperCase() === 'PROCESSANDO');
    const totalLotes = lotes.length;
    const progresso = Math.round((lotesFinalizados.length / totalLotes) * 100);
    const horaInicio = lotes[0].dataInicio ? formatTime(lotes[0].dataInicio) : '-';
    let horaFim = '-';
    let duracao = '-';
    if (lotesFinalizados.length === totalLotes) {
        const fim = lotesFinalizados[lotesFinalizados.length - 1].dataFim || lotesFinalizados[lotesFinalizados.length - 1].dataHora;
        if (fim) {
            horaFim = formatTime(fim);
            duracao = formatDuration(lotes[0].dataInicio, fim);
        }
    } else if (loteAtual && loteAtual.dataInicio) {
        horaFim = '-';
        duracao = formatDuration(lotes[0].dataInicio, new Date());
    }
    if (resumoIdUpload) resumoIdUpload.textContent = idUpload;
    if (resumoDataInicio) resumoDataInicio.textContent = dataInicio;
    if (resumoQtdLotes) resumoQtdLotes.textContent = qtdLotes;
    if (resumoLoteProgresso) resumoLoteProgresso.textContent = `${lotesFinalizados.length + (loteAtual ? 1 : 0)} de ${totalLotes}`;
    if (resumoLoteProgressbar) resumoLoteProgressbar.style.width = `${progresso}%`;
    if (resumoHoraInicio) resumoHoraInicio.textContent = horaInicio;
    if (resumoHoraFim) resumoHoraFim.textContent = horaFim;
    if (resumoDuracao) resumoDuracao.textContent = duracao;
}

function atualizarCardLoteAtual(lotes) {
    const cardLoteAtual = document.getElementById('cardLoteAtual');
    if (!cardLoteAtual) return null;
    // Se todos os lotes estão finalizados, oculta o card
    const todosFinalizados = lotes.every(l => l.status && (l.status.toUpperCase() === 'FINALIZADO' || l.status.toUpperCase() === 'CONCLUIDO' || l.status.toUpperCase() === 'ERRO'));
    if (todosFinalizados) {
        cardLoteAtual.style.display = 'none';
        cardLoteAtual.innerHTML = '';
        return null;
    } else {
        cardLoteAtual.style.display = 'block';
    }
    const loteAtualIdx = lotes.findIndex(l => l.status && l.status.toUpperCase() === 'PROCESSANDO');
    const loteAtual = loteAtualIdx >= 0 ? lotes[loteAtualIdx] : null;
    const loteAtualNum = loteAtualIdx >= 0 ? (loteAtualIdx + 1) : lotes.length;
    cardLoteAtual.innerHTML = '';
    if (loteAtual) {
        const tpl = document.getElementById('tpl-lote-atual');
        if (tpl) {
            const node = tpl.content.cloneNode(true);
            node.querySelector('.tpl-num').textContent = loteAtualNum;
            node.querySelector('.tpl-qtd-arq').textContent = loteAtual.totalArquivos || (loteAtual.arquivos ? loteAtual.arquivos.length : 0);
            node.querySelector('.tpl-usuario').textContent = loteAtual.usuario || '-';
            node.querySelector('.tpl-inicio').textContent = loteAtual.dataInicio ? formatDateFull(loteAtual.dataInicio) : '-';
            node.querySelector('.tpl-fim').textContent = loteAtual.dataFim ? formatDateFull(loteAtual.dataFim) : '-';
            node.querySelector('.tpl-duracao').textContent = loteAtual.dataInicio && loteAtual.dataFim ? formatDuration(loteAtual.dataInicio, loteAtual.dataFim) : (loteAtual.dataInicio ? formatDuration(loteAtual.dataInicio, new Date()) : '-');
            const arquivosProc = loteAtual.arquivosProcessados || 0;
            const totalArquivosLote = loteAtual.totalArquivos || (loteAtual.arquivos ? loteAtual.arquivos.length : 0);
            node.querySelector('.tpl-arquivo-atual').textContent = loteAtual.arquivoAtual ? `Processando arquivo ${loteAtual.arquivoAtual} de ${totalArquivosLote}` : `Processando arquivo ${arquivosProc + 1} de ${totalArquivosLote}`;
            const progressoLote = Math.round((arquivosProc / (totalArquivosLote || 1)) * 100);
            const statusClass = getStatusClass(loteAtual.status);
            const statusText = getStatusText(loteAtual.status);
            const bar = node.querySelector('.tpl-bar');
            if (bar) {
                bar.style.width = `${progressoLote}%`;
                bar.setAttribute('aria-valuenow', progressoLote);
                bar.className = `progress-bar tpl-bar ${statusClass}`;
                bar.textContent = `${progressoLote}%`;
            }
            const badge = node.querySelector('.tpl-status');
            if (badge) {
                badge.className = `badge tpl-status ${statusClass}`;
                badge.textContent = statusText;
            }
            node.querySelector('.tpl-msg').textContent = loteAtual.mensagem || '-';
            cardLoteAtual.appendChild(node);
        }
        return loteAtual;
    } else {
        cardLoteAtual.innerHTML = '<span class="text-muted">Nenhum lote em processamento.</span>';
        return null;
    }
}

function adicionarLoteAoHistorico(lote, idx) {
    const cardHistoricoLotes = document.getElementById('cardHistoricoLotes');
    if (!cardHistoricoLotes || !lote) return;
    const tpl = document.getElementById('tpl-historico-lote');
    if (!tpl) return;
    const node = tpl.content.cloneNode(true);
    const wrapper = node.firstElementChild;
    if (!wrapper) return;
    // Preenche campos principais
    const numEl = wrapper.querySelector('.tpl-num');
    if (numEl) numEl.textContent = idx + 1;
    const usuarioEl = wrapper.querySelector('.tpl-usuario');
    if (usuarioEl) usuarioEl.textContent = lote.usuario || '-';
    const inicioEl = wrapper.querySelector('.tpl-inicio');
    if (inicioEl) inicioEl.textContent = lote.dataInicio ? formatTime(lote.dataInicio) : '-';
    const fimEl = wrapper.querySelector('.tpl-fim');
    if (fimEl) fimEl.textContent = lote.dataFim ? formatTime(lote.dataFim) : '-';
    const duracaoEl = wrapper.querySelector('.tpl-duracao');
    if (duracaoEl) duracaoEl.textContent = lote.dataInicio && lote.dataFim ? formatDuration(lote.dataInicio, lote.dataFim) : '-';
    const statusClass = getStatusClass(lote.status);
    const statusText = getStatusText(lote.status);
    const badge = wrapper.querySelector('.tpl-status');
    if (badge) {
        badge.className = `badge tpl-status ${statusClass}`;
        badge.textContent = statusText;
    }
    const qtdArqEl = wrapper.querySelector('.tpl-qtd-arq');
    if (qtdArqEl) qtdArqEl.textContent = lote.totalArquivos || (lote.arquivos ? lote.arquivos.length : 0);
    // Mensagem + nomes de arquivos (usando lote.nomeArquivos se existir)
    let msg = lote.mensagem || '-';
    

    // lote.arquivos = appState.lotes_uploads.find(l => l.id === lote.idLote).arquivos;

 


    // if (Array.isArray(lote.nomeArquivos) && lote.nomeArquivos.length > 0) {
    //     nomes = lote.nomeArquivos;
    // } else if (lote.arquivos && Array.isArray(lote.arquivos) && lote.arquivos.length > 0) {
    //     nomes = lote.arquivos.map(a => a.FileName || a.nomeArquivo || a.fileName || a.nome || '').filter(Boolean);
    // }

    lote.arquivos = appState.lotes_uploads.find(l => l.id === lote.idLote).arquivos;
    
    const nomes = lote.arquivos
        .slice(0, 3)
        .map(file => file.name)
        .join(', ');

    const mais = lote.arquivos.length > 3 ? `... e mais ${lote.arquivos.length - 3} arquivos do lote` : '';
    msg = `${msg} (${nomes}${mais ? ', ' + mais : ''})`;

    const msgEl = wrapper.querySelector('.tpl-msg');
    if (msgEl) msgEl.textContent = msg;
    cardHistoricoLotes.appendChild(wrapper);
}

function atualizarTodosCards(lotes, loteAtual) {
    // Atualiza o lote atual
    atualizarCardLoteAtual(lotes);

    if(loteAtual)  {
        console.log(loteAtual.status, loteAtual)
    }
    // Se o lote atual acabou de ser finalizado, adiciona ao histórico
    // (verifica se algum lote mudou para CONCLUIDO ou ERRO e ainda não está no histórico)
    if (loteAtual && (loteAtual.status && (loteAtual.status.toUpperCase() === 'CONCLUIDO' || loteAtual.status.toUpperCase() === 'ERRO'))) {
        adicionarLoteAoHistorico(loteAtual, lotes.findIndex(l => l.idLote === loteAtual.idLote));
    }
    // Atualiza o resumo sempre
    atualizarCardResumo(lotes);
}

// Funções auxiliares (pode importar do arquivo principal se preferir)
function formatDateFull(dateString) {
    if (!dateString) return '-';
    const date = new Date(dateString);
    return date.toLocaleString();
}
function formatTime(dateString) {
    if (!dateString) return '-';
    const date = new Date(dateString);
    return date.toLocaleTimeString();
}
// As funções getStatusClass, getStatusText, formatDuration devem estar disponíveis no escopo global ou importadas

function resetarCards() {
    // Limpa resumo
    atualizarCardResumo([]);
    // Limpa lote atual e mostra novamente
    const cardLoteAtual = document.getElementById('cardLoteAtual');
    if (cardLoteAtual) {
        cardLoteAtual.innerHTML = '';
        cardLoteAtual.style.display = 'block';
    }
    // Limpa histórico e mostra
    const cardHistoricoLotes = document.getElementById('cardHistoricoLotes');
    if (cardHistoricoLotes) {
        cardHistoricoLotes.innerHTML = '';
        cardHistoricoLotes.style.display = 'block';
    }
    // Mostra card resumo
    const cardResumoGeral = document.getElementById('cardResumoGeral');
    if (cardResumoGeral) cardResumoGeral.style.display = 'block';
}

// Exporte as funções principais
window.BatchCardsRenderer = {
    atualizarCardLoteAtual,
    atualizarTodosCards,
    resetarCards
};

// ========== wwwroot\js\testData.js ==========
document.addEventListener('DOMContentLoaded', function() {
    const form = document.getElementById('testDataForm');
    const statusDiv = document.getElementById('status');

    form.addEventListener('submit', async function(e) {
        e.preventDefault();

        const quantidadeArquivos = document.getElementById('quantidadeArquivos').value;
        const linhasPorArquivo = document.getElementById('linhasPorArquivo').value;

        try {
            statusDiv.style.display = 'block';
            statusDiv.innerHTML = 'Gerando arquivos de teste...';
            
            const response = await fetch('/api/TestDataApi/gerar', {
                method: 'POST',
                headers: {
                    'Content-Type': 'application/json'
                },
                body: JSON.stringify({
                    quantidadeArquivos: parseInt(quantidadeArquivos),
                    linhasPorArquivo: parseInt(linhasPorArquivo)
                })
            });

            const result = await response.json();

            if (response.ok) {
                statusDiv.className = 'alert alert-success';
                statusDiv.innerHTML = `Arquivos gerados com sucesso!<br>${result.mensagem}`;
            } else {
                statusDiv.className = 'alert alert-danger';
                statusDiv.innerHTML = `Erro ao gerar arquivos: ${result.erro}`;
            }
        } catch (error) {
            statusDiv.className = 'alert alert-danger';
            statusDiv.innerHTML = `Erro ao gerar arquivos: ${error.message}`;
        }
    });
});

