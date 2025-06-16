// Estado da aplicação
const appState = {
    currentBatchId: null,
    batches: [],
    currentPage: 1,
    itemsPerPage: 20,
    hasProcessingBatches: false,
    selectedBatchId: null
};

// Elementos do DOM
const uploadForm = document.getElementById('uploadForm');
const fileInput = document.getElementById('fileInput');
const selectedFiles = document.getElementById('selectedFiles');
const batchTable = document.getElementById('batchTable');
const fileTable = document.getElementById('fileTable');
const batchTableBody = document.getElementById('batchTableBody');
const fileTableBody = document.getElementById('fileTableBody');
const batchInfo = document.getElementById('batchInfo');
const prevPageBtn = document.getElementById('prevPage');
const nextPageBtn = document.getElementById('nextPage');
const pageInfo = document.getElementById('pageInfo');
const downloadBtn = document.getElementById('downloadBtn');
const generateTestBtn = document.getElementById('generateTestBtn');
const errorModal = document.getElementById('errorModal');
const errorModalTitle = document.getElementById('errorModalTitle');
const errorModalBody = document.getElementById('errorModalBody');
const closeErrorModalBtn = document.getElementById('closeErrorModalBtn');

// Inicialização
document.addEventListener('DOMContentLoaded', () => {
    if (uploadForm) {
        uploadForm.addEventListener('submit', handleUpload);
    }
    if (fileInput) {
        fileInput.addEventListener('change', updateSelectedFiles);
    }
    if (prevPageBtn) {
        prevPageBtn.addEventListener('click', () => changePage(-1));
    }
    if (nextPageBtn) {
        nextPageBtn.addEventListener('click', () => changePage(1));
    }
    if (downloadBtn) {
        downloadBtn.addEventListener('click', handleDownload);
    }
    if (generateTestBtn) {
        generateTestBtn.addEventListener('click', generateTestFiles);
    }
    if (closeErrorModalBtn) {
        closeErrorModalBtn.addEventListener('click', () => {
            const modal = bootstrap.Modal.getInstance(errorModal);
            if (modal) {
                modal.hide();
            }
        });
    }

    // Inicia o polling
    pollBatches();
});

// Funções de manipulação de eventos
function updateSelectedFiles() {
    if (selectedFiles && fileInput.files.length > 0) {
        const files = Array.from(fileInput.files);
        const totalFiles = files.length;
        const displayCount = Math.min(4, totalFiles);
        
        const fileList = files
            .slice(0, displayCount)
            .map(file => file.name)
            .join(', ');
            
        const remainingCount = totalFiles - displayCount;
        selectedFiles.textContent = remainingCount > 0 
            ? `${fileList}... mais ${remainingCount} arquivos de um total de ${totalFiles}`
            : fileList;
    } else {
        selectedFiles.textContent = '';
    }
}

async function handleUpload(e) {
    e.preventDefault();
    if (!fileInput.files.length) return;

    try {
        // Primeiro, inicia um novo batch
        const batchResponse = await fetch('/api/upload/startbatch', {
            method: 'POST',
            headers: {
                'Content-Type': 'application/json'
            }
        });

        if (!batchResponse.ok) {
            throw new Error('Erro ao iniciar batch');
        }

        const batchResult = await batchResponse.json();
        appState.currentBatchId = batchResult;
        appState.selectedBatchId = batchResult;

        // Agora, processa cada arquivo individualmente
        const files = Array.from(fileInput.files);
        for (const file of files) {
            const formData = new FormData();
            formData.append('file', file);
            formData.append('batchId', batchResult);

            const uploadResponse = await fetch('/api/upload/uploadfile', {
                method: 'POST',
                body: formData
            });

            if (!uploadResponse.ok) {
                console.error(`Erro ao fazer upload do arquivo ${file.name}`);
            }
        }

        fileInput.value = '';
        selectedFiles.textContent = '';
        
        // Atualiza imediatamente após o upload
        await pollBatches();
    } catch (error) {
        console.error('Erro no upload:', error);
        alert('Erro ao fazer upload dos arquivos');
    }
}

async function handleDownload() {
    const selectedFile = document.querySelector('input[name="selectedFile"]:checked');
    if (!selectedFile) {
        alert('Selecione um arquivo para download');
        return;
    }

    const fileName = selectedFile.value;
    window.location.href = `/api/upload/download/${encodeURIComponent(fileName)}`;
}

async function generateTestFiles() {
    try {
        const response = await fetch('/api/upload/generatetestfiles', {
            method: 'POST',
            headers: {
                'Content-Type': 'application/json'
            }
        });

        if (!response.ok) {
            throw new Error('Erro ao gerar arquivos de teste');
        }

        const result = await response.json();
        appState.currentBatchId = result.batchId;
        appState.selectedBatchId = result.batchId;
        
        // Atualiza imediatamente após a geração
        await pollBatches();
    } catch (error) {
        console.error('Erro ao gerar arquivos de teste:', error);
        alert('Erro ao gerar arquivos de teste');
    }
}

function changePage(delta) {
    const newPage = appState.currentPage + delta;
    const totalPages = Math.ceil(appState.batches.length / appState.itemsPerPage);
    
    if (newPage >= 1 && newPage <= totalPages) {
        appState.currentPage = newPage;
        updateFileTable();
    }
}

// Funções de atualização da UI
async function pollBatches() {
    try {
        const response = await fetch('/api/upload/getallbatches');
        if (!response.ok) {
            throw new Error('Erro ao obter lotes');
        }

        const batches = await response.json();
        appState.batches = batches;
        
        // Verifica se há lotes em processamento
        appState.hasProcessingBatches = batches.some(batch => 
            batch.status === "PROCESSANDO" || 
            batch.arquivos.some(file => file.status === "UPLOADING" || file.status === "PROCESSANDO")
        );

        updateBatchTable();
        updateFileTable();

        // Ajusta o intervalo de polling baseado no estado
        setTimeout(pollBatches, appState.hasProcessingBatches ? 1000 : 5000);
    } catch (error) {
        console.error('Erro ao obter lotes:', error);
        // Continua o polling mesmo em caso de erro, mas com um intervalo maior
        setTimeout(pollBatches, 10000);
    }
}

function updateBatchTable() {
    if (!batchTableBody) return;

    batchTableBody.innerHTML = '';
    appState.batches.forEach((batch, index) => {
        const progress = batch.totalArquivos > 0 
            ? Math.round((batch.arquivosProcessados / batch.totalArquivos) * 100) 
            : 0;
        
        const row = document.createElement('tr');
        row.className = appState.selectedBatchId === batch.id ? 'table-primary' : '';
        row.style.cursor = 'pointer';
        row.onclick = () => selectBatch(batch.id);
        
        row.innerHTML = `
            <td>${index + 1}</td>
            <td><span class="badge ${getStatusClass(batch.status)}">${batch.status}</span></td>
            <td>
                <div class="progress">
                    <div class="progress-bar ${getStatusClass(batch.status)}" role="progressbar" 
                         style="width: ${progress}%" 
                         aria-valuenow="${progress}" 
                         aria-valuemin="0" 
                         aria-valuemax="100">
                        ${progress}%
                    </div>
                </div>
            </td>
            <td>${batch.arquivosProcessados}/${batch.totalArquivos}</td>
            <td>${new Date(batch.dataCriacao).toLocaleString()}</td>
        `;
        batchTableBody.appendChild(row);
    });
}

function selectBatch(batchId) {
    appState.selectedBatchId = batchId;
    updateBatchTable();
    updateFileTable();
}

function updateFileTable() {
    if (!fileTableBody) return;

    fileTableBody.innerHTML = '';
    
    // Se um lote está selecionado, mostra seus arquivos
    let filesToShow = [];
    if (appState.selectedBatchId) {
        const selectedBatch = appState.batches.find(b => b.id === appState.selectedBatchId);
        if (selectedBatch) {
            filesToShow = selectedBatch.arquivos;
        }
    } else {
        // Caso contrário, mostra os últimos 20 arquivos sendo processados
        filesToShow = appState.batches
            .flatMap(batch => batch.arquivos)
            .sort((a, b) => new Date(b.dataInicio) - new Date(a.dataInicio))
            .slice(0, appState.itemsPerPage);
    }

    filesToShow.forEach(file => {
        const tr = document.createElement('tr');
        const statusHtml = file.status === 'ERRO' 
            ? `<a href="#" class="text-decoration-none" onclick="showErrorDetails('${file.nomeArquivo}', '${encodeURIComponent(file.mensagem)}')">
                ${getStatusBadge(file.status)}
               </a>`
            : getStatusBadge(file.status);

        tr.innerHTML = `
            <td>${file.nomeArquivo}</td>
            <td>${statusHtml}</td>
            <td>${file.dataInicio ? new Date(file.dataInicio).toLocaleString() : '-'}</td>
            <td>${file.dataFim ? new Date(file.dataFim).toLocaleString() : '-'}</td>
            <td>
                ${file.status === 'CONCLUIDO' ? 
                    `<button class="btn btn-sm btn-success" onclick="downloadFile('${file.nomeArquivo}')">
                        <i class="bi bi-download"></i> Download
                    </button>` : 
                    '-'
                }
            </td>
        `;
        fileTableBody.appendChild(tr);
    });

    // Atualiza informações de paginação
    if (pageInfo) {
        const totalFiles = appState.selectedBatchId 
            ? appState.batches.find(b => b.id === appState.selectedBatchId)?.arquivos.length || 0
            : appState.batches.reduce((sum, batch) => sum + batch.arquivos.length, 0);
            
        const hasMoreFiles = totalFiles > appState.itemsPerPage;
        pageInfo.textContent = hasMoreFiles ? "Mais arquivos..." : "";
    }
}

function getStatusClass(status) {
    switch (status) {
        case "FINALIZADO": return "bg-success";
        case "ERRO": return "bg-danger";
        case "PROCESSANDO": return "bg-primary";
        case "UPLOADING": return "bg-info";
        case "CANCELADO": return "bg-secondary";
        default: return "bg-warning";
    }
}

function getStatusBadge(status) {
    switch (status) {
        case 'CONCLUIDO':
            return `<span class="badge bg-success">${status}</span>`;
        case 'ERRO':
            return `<span class="badge bg-danger">${status}</span>`;
        case 'UPLOADING':
            return `<span class="badge bg-primary">${status}</span>`;
        case 'PROCESSANDO':
            return `<span class="badge bg-warning text-dark">${status}</span>`;
        case 'PENDENTE':
            return `<span class="badge bg-secondary">${status}</span>`;
        default:
            return `<span class="badge bg-secondary">${status}</span>`;
    }
}

function showErrorDetails(fileName, errorMessage) {
    const modal = new bootstrap.Modal(document.getElementById('errorModal'));
    document.getElementById('errorFileName').textContent = fileName;
    document.getElementById('errorDetails').textContent = decodeURIComponent(errorMessage);
    modal.show();
}
