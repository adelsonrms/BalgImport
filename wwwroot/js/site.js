let files = [];
let pollingInterval = null;

document.getElementById('fileUpload').addEventListener('change', function (event) {
    files = Array.from(event.target.files);
    const nomes = files.slice(0, 5).map(f => f.name);
    const restantes = files.length > 5 ? `+${files.length - 5} arquivos` : '';

    document.getElementById('arquivosSelecionados').innerHTML = `
      <div class="alert alert-info">
        <strong>Arquivos Selecionados:</strong><br/>
        <ul class="mb-1">${nomes.map(n => `<li>${n}</li>`).join('')}</ul>
        ${restantes ? `<div><em>${restantes}</em></div>` : ''}
      </div>`;
});

function startUpload() {
    if (files.length === 0) {
        alert('Selecione ao menos um arquivo!');
        return;
    }

    files.forEach(file => uploadFile(file));

    // Inicia o polling apenas se não estiver rodando
    if (!pollingInterval) {
        pollingInterval = setInterval(loadStatus, 2000);
    }
}

function uploadFile(file) {
    const formData = new FormData();
    formData.append('file', file);

    fetch('/api/upload/uploadfile', {
        method: 'POST',
        body: formData
    })
        .then(res => {
            if (!res.ok) throw new Error(`Erro HTTP! Status: ${res.status}`);
            return res.json();
        })
        .then(data => {
            console.log(`${file.name} upload iniciado`);
        })
        .catch(err => {
            console.error('Erro no upload:', err);
        });
}

function loadStatus() {
    fetch('/api/upload/getstatus')
        .then(res => {
            if (!res.ok) throw new Error(`Erro HTTP! Status: ${res.status}`);
            return res.json();
        })
        .then(data => {
            const tbody = document.getElementById('statusTableBody');
            tbody.innerHTML = '';

            let arquivosPendentes = 0;

            data.forEach(item => {
                const emAndamento = !(item.status === 'FINALIZADO' || item.status === 'ERRO');
                if (emAndamento) arquivosPendentes++;

                let badge = '';
                switch (item.status) {
                    case 'UPLOADING':
                        badge = '<span class="badge bg-info">Uploading</span>'; break;
                    case 'PROCESSANDO_SQL':
                        badge = '<span class="badge bg-warning text-dark">Processando SQL</span>'; break;
                    case 'FINALIZADO':
                        badge = '<span class="badge bg-success">Finalizado</span>'; break;
                    case 'ERRO':
                        badge = '<span class="badge bg-danger">Erro</span>'; break;
                    default:
                        badge = item.status;
                }

                const progresso = `
                    <div class="progress">
                      <div class="progress-bar 
                        ${item.status === 'FINALIZADO' ? 'bg-success' :
                        item.status === 'ERRO' ? 'bg-danger' : 'bg-info'}" 
                        role="progressbar" style="width: ${item.progresso}%" 
                        aria-valuenow="${item.progresso}" aria-valuemin="0" aria-valuemax="100">
                        ${item.progresso}%
                      </div>
                    </div>`;

                tbody.innerHTML += `
                  <tr>
                    <td>${item.nomeArquivo}</td>
                    <td>${badge}</td>
                    <td style="min-width:200px">${progresso}</td>
                    <td>${item.mensagem ?? ''}</td>
                    <td>${new Date(item.dataInicio).toLocaleTimeString()}</td>
                    <td>${item.dataFim ? new Date(item.dataFim).toLocaleTimeString() : '-'}</td>
                  </tr>`;
            });

            // Se não há mais arquivos pendentes, para o polling
            if (arquivosPendentes === 0) {
                clearInterval(pollingInterval);
                pollingInterval = null;
                console.log('✅ Todos os uploads processados. Polling encerrado.');
            }
        })
        .catch(err => {
            console.error('Erro carregando status:', err);
            clearInterval(pollingInterval);
            pollingInterval = null;
        });
}
