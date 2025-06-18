// Estado da aplicação
const appState = {
    selectedBatchId: null,
    hasProcessingBatches: false,
    connection: null
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

// Função para lidar com o upload
let currentBatchId = null;
let batchCounter = 1;

async function handleUpload() {
    const fileInput = document.getElementById('files');
    const files = fileInput.files;

    if (!files || files.length === 0) {
        showError('Selecione pelo menos um arquivo');
        return;
    }

    try {
        console.log('Iniciando upload de arquivos...');
        
        const formData = new FormData();
        for (let i = 0; i < files.length; i++) {
            formData.append('arquivos', files[i]);
            console.log(`Adicionando arquivo ${i + 1}: ${files[i].name}`);
        }

        const response = await fetch('/api/ImportacaoApi/upload', {
            method: 'POST',
            body: formData
        });

        if (!response.ok) {
            const error = await response.json();
            throw new Error(error.erro || 'Erro ao fazer upload');
        }

        const result = await response.json();
        console.log('Upload iniciado com sucesso:', result);

        // Limpa o input de arquivos
        fileInput.value = '';
    } catch (error) {
        console.error('Erro no upload:', error);
        showError(error.message || 'Erro ao fazer upload dos arquivos');
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
    const row = document.getElementById(`batch-${batchId}`);
    if (row) {
        const statusBadge = row.querySelector('.badge');
        const statusText = getStatusText(status);
        const statusClass = getStatusClass(status);
        
        row.children[1].textContent = `${processedFiles} / ${totalFiles}`;
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
    const statusMap = {
        'CONCLUIDO': 'Concluído',
        'ERRO': 'Erro',
        'PROCESSANDO': 'Processando',
        'PENDENTE': 'Pendente',
        'CANCELADO': 'Cancelado'
    };
    return statusMap[status] || status;
}

function getStatusClass(status) {
    const classMap = {
        'Uploading': 'bg-info',
        'PROCESSANDO': 'bg-warning',
        'CONCLUIDO': 'bg-success',
        'ERRO': 'bg-danger',
        'PENDENTE': 'bg-secondary',
        'CANCELADO': 'bg-dark'
    };
    return classMap[status] || 'bg-secondary';
}

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
            
            console.log(`Status do batch ${batchId}: ${batch.status}`);
            
            // Atualiza o status do lote
            const processedFiles = batch.arquivos.filter(f => f.status === 'CONCLUIDO' || f.status === 'ERRO').length;
            updateBatchStatus(
                batchId,
                processedFiles,
                batch.arquivos.length,
                batch.status
            );

            // Atualiza o status dos arquivos
            batch.arquivos.forEach(file => {
                if (!document.getElementById(`file-${file.nomeArquivo}`)) {
                    addFileToTable(file.nomeArquivo, file.status);
                }
                updateFileStatus(file.nomeArquivo, file.status, file.mensagemErro);
            });

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
                    const statusClass = getBatchStatusClass(batchStatus);
                    const statusText = getBatchStatusText(batchStatus);
                    
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
        const statusClass = arquivo.status === 'CONCLUIDO' ? 'bg-success' : 
                          arquivo.status === 'ERRO' ? 'bg-danger' : 
                          arquivo.status === 'PROCESSANDO' ? 'bg-warning' : 'bg-secondary';

        // Extrai empresa e layout do nome do arquivo
        const [empresa, layout] = arquivo.nomeArquivo.split('_');

        row.innerHTML = `
            <td>${arquivo.nomeArquivo}</td>
            <td>${empresa || '-'}</td>
            <td>${layout || '-'}</td>
            <td><span class="badge ${statusClass}">${getStatusText(arquivo.status)}</span></td>
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
    return date.toLocaleString('pt-BR');
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
        const statusClass = getBatchStatusClass(batch.status);
        const statusText = getBatchStatusText(batch.status);
        
        row.innerHTML = `
            <td>#${batchCounter++}</td>
            <td>${batch.arquivosProcessados} / ${batch.totalArquivos}</td>
            <td>${formatDate(batch.dataInicio)}</td>
            <td><span class="badge ${statusClass}">${statusText}</span></td>
        `;

        batchTableBody.appendChild(row);
    });
}

function getBatchStatusClass(status) {
    switch (status) {
        case 'CONCLUIDO':
            return 'bg-success';
        case 'ERRO':
            return 'bg-danger';
        case 'PROCESSANDO':
            return 'bg-warning';
        case 'CANCELADO':
            return 'bg-dark';
        default:
            return 'bg-secondary';
    }
}

function getBatchStatusText(status) {
    switch (status) {
        case 'CONCLUIDO':
            return 'Concluído';
        case 'ERRO':
            return 'Erro';
        case 'PROCESSANDO':
            return 'Em andamento';
        case 'CANCELADO':
            return 'Cancelado';
        case 'PENDENTE':
            return 'Pendente';
        default:
            return status;
    }
}

function getBatchStatus(batch) {
    // Se não houver arquivos, retorna pendente
    if (!batch.arquivos || batch.arquivos.length === 0) {
        return 'PENDENTE';
    }

    // Conta os arquivos por status
    const statusCount = batch.arquivos.reduce((acc, arquivo) => {
        acc[arquivo.status] = (acc[arquivo.status] || 0) + 1;
        return acc;
    }, {});

    // Se todos os arquivos estão concluídos
    if (statusCount['CONCLUIDO'] === batch.arquivos.length) {
        return 'CONCLUIDO';
    }

    // Se todos os arquivos estão com erro
    if (statusCount['ERRO'] === batch.arquivos.length) {
        return 'ERRO';
    }

    // Se houver pelo menos um arquivo em processamento
    if (statusCount['PROCESSANDO'] > 0) {
        return 'PROCESSANDO';
    }

    // Se houver pelo menos um arquivo pendente
    if (statusCount['PENDENTE'] > 0) {
        return 'PENDENTE';
    }

    // Se houver pelo menos um arquivo com erro
    if (statusCount['ERRO'] > 0) {
        return 'ERRO';
    }

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
        atualizarTabelas(data);
    });

    // Inicia a conexão
    appState.connection.start()
        .then(() => {
            console.log('SignalR: Conexão estabelecida com sucesso');
            // Carrega os dados iniciais apenas uma vez
            loadInitialData();
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
                    
                    const statusClass = getBatchStatusClass(batch.status);
                    const statusText = getBatchStatusText(batch.status);
                    
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
        const statusClass = getFileStatusClass(arquivo.status);
        const statusText = getFileStatusText(arquivo.status);
        
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

function getFileStatusClass(status) {
    switch (status?.toUpperCase()) {
        case 'PENDENTE': return 'bg-warning';
        case 'PROCESSANDO': return 'bg-info';
        case 'CONCLUIDO': return 'bg-success';
        case 'ERRO': return 'bg-danger';
        default: return 'bg-secondary';
    }
}

function getFileStatusText(status) {
    switch (status?.toUpperCase()) {
        case 'PENDENTE': return 'Pendente';
        case 'PROCESSANDO': return 'Processando';
        case 'CONCLUIDO': return 'Concluído';
        case 'ERRO': return 'Erro';
        default: return 'Desconhecido';
    }
}

function atualizarTabelas(status) {
    try {
        console.log('Atualizando tabelas com status:', status);

        // Validação dos dados
        if (!status) {
            console.error('Status inválido:', status);
            return;
        }

        // Atualiza a tabela de lotes
        const batchTableBody = document.getElementById('batchTableBody');
        if (!batchTableBody) {
            console.error('Elemento batchTableBody não encontrado');
            return;
        }

        // Procura ou cria a linha do lote
        let batchRow = document.querySelector(`#batchTableBody tr[data-batch-id="${status.idLote}"]`);
        if (!batchRow) {
            batchRow = document.createElement('tr');
            batchRow.setAttribute('data-batch-id', status.idLote);
            batchTableBody.insertBefore(batchRow, batchTableBody.firstChild);
        }

        // Calcula o progresso
        const totalArquivos = status.totalArquivos || 1;
        const arquivosProcessados = status.arquivosProcessados || 0;
        const progresso = Math.round((arquivosProcessados / totalArquivos) * 100);

        // Atualiza os dados do lote
        const dataHora = status.dataHora ? new Date(status.dataHora).toLocaleString('pt-BR') : '-';
        const statusClass = getStatusClass(status.status);
        const statusText = getStatusText(status.status);
        
        batchRow.innerHTML = `
            <td>${status.idUpload || '-'}</td>
            <td>${status.idLote || '-'}</td>
            <td>${arquivosProcessados} / ${totalArquivos}</td>
            <td>
                <div class="progress">
                    <div class="progress-bar" role="progressbar" 
                         style="width: ${progresso}%" 
                         aria-valuenow="${progresso}" 
                         aria-valuemin="0" 
                         aria-valuemax="100">
                        ${progresso}%
                    </div>
                </div>
            </td>
            <td><span class="badge ${statusClass}">${statusText}</span></td>
            <td>${status.mensagem || '-'}</td>
            <td>${dataHora}</td>
        `;

        console.log('Tabela de lotes atualizada com sucesso');
    } catch (error) {
        console.error('Erro ao atualizar tabelas:', error);
    }
}

function getStatusClass(status) {
    if (!status) return 'bg-secondary';
    
    switch (status.toUpperCase()) {
        case 'CONCLUIDO':
            return 'bg-success';
        case 'PROCESSANDO':
            return 'bg-warning';
        case 'ERRO':
            return 'bg-danger';
        case 'PENDENTE':
            return 'bg-secondary';
        default:
            return 'bg-secondary';
    }
}

function getStatusText(status) {
    if (!status) return 'Desconhecido';
    
    switch (status.toUpperCase()) {
        case 'CONCLUIDO':
            return 'Concluído';
        case 'PROCESSANDO':
            return 'Processando';
        case 'ERRO':
            return 'Erro';
        case 'PENDENTE':
            return 'Pendente';
        default:
            return status;
    }
}
