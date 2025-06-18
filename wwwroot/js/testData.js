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