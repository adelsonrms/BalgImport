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