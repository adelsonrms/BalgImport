
/*************************************************************
 * Script Completo - Processo BALG
 * Empresa: BRADESCO CONTADORIA
 * Objetivo: Estruturar processamento de arquivos BALG
 *************************************************************/

-------------------------------------------------------------
-- 🗂️ 1. Tabelas
-------------------------------------------------------------
IF OBJECT_ID('dbo.STG_BALG', 'U') IS NOT NULL DROP TABLE dbo.STG_BALG;
CREATE TABLE dbo.STG_BALG (
    C_ID_UPLOAD INT,
    NOME_ARQUIVO VARCHAR(255),
    D_BASE DATE,
    CD_EMP VARCHAR(20),
    CD_CONTA VARCHAR(50),
    PRZ VARCHAR(10),
    MOE VARCHAR(10),
    SLD DECIMAL(25, 2),
    DT_IMPORTACAO DATETIME DEFAULT GETDATE(),
    FLAG_ERRO BIT DEFAULT 0,
    MENSAGEM_ERRO VARCHAR(MAX) NULL
);
GO

IF OBJECT_ID('dbo.T_CTRL_UPLOAD_BALG', 'U') IS NOT NULL DROP TABLE dbo.T_CTRL_UPLOAD_BALG;
CREATE TABLE dbo.T_CTRL_UPLOAD_BALG (
    ID_UPLOAD INT IDENTITY(1,1) PRIMARY KEY,
    NOME_ARQUIVO VARCHAR(255),
    STATUS VARCHAR(50),
    DT_INICIO DATETIME DEFAULT GETDATE(),
    DT_FIM DATETIME NULL,
    MENSAGEM VARCHAR(MAX) NULL
);
GO

IF OBJECT_ID('dbo.T_LOG_PROCESSAMENTO', 'U') IS NOT NULL DROP TABLE dbo.T_LOG_PROCESSAMENTO;
CREATE TABLE dbo.T_LOG_PROCESSAMENTO (
    ID_LOG INT IDENTITY(1,1) PRIMARY KEY,
    ID_UPLOAD INT,
    DATA_LOG DATETIME DEFAULT GETDATE(),
    ETAPA VARCHAR(100),
    STATUS VARCHAR(50),
    MENSAGEM VARCHAR(MAX) NULL
);
GO

IF OBJECT_ID('dbo.T_PCONP_BASE_CONSL_ANLTCA', 'U') IS NULL
CREATE TABLE dbo.T_PCONP_BASE_CONSL_ANLTCA (
    NOME_ARQUIVO VARCHAR(255),
    CD_EMP VARCHAR(20),
    CD_CONTA VARCHAR(50),
    PRZ VARCHAR(10),
    MOE VARCHAR(10),
    SLD DECIMAL(25,2),
    D_BASE DATE
);
GO

-------------------------------------------------------------
-- 🔥 Procedures Modulares
-------------------------------------------------------------

-- 1. PROC_CARGA_BALG - Ler CSV, montar staging
IF OBJECT_ID('dbo.PROC_CARGA_BALG', 'P') IS NOT NULL DROP PROCEDURE dbo.PROC_CARGA_BALG;
GO

CREATE PROCEDURE dbo.PROC_CARGA_BALG
    @ID_UPLOAD INT,
    @NOME_ARQUIVO VARCHAR(255)
AS
BEGIN
    SET NOCOUNT ON;

    INSERT INTO dbo.T_LOG_PROCESSAMENTO (ID_UPLOAD, ETAPA, STATUS, MENSAGEM)
    VALUES (@ID_UPLOAD, 'CARGA', 'INFO', 'Carga na STAGING concluída (dados simulados).');
END
GO

-- 2. PROC_VALIDA_BALG - Defasagem, moeda, layouts
IF OBJECT_ID('dbo.PROC_VALIDA_BALG', 'P') IS NOT NULL DROP PROCEDURE dbo.PROC_VALIDA_BALG;
GO

CREATE PROCEDURE dbo.PROC_VALIDA_BALG
    @ID_UPLOAD INT
AS
BEGIN
    SET NOCOUNT ON;

    UPDATE STG_BALG
    SET FLAG_ERRO = 1,
        MENSAGEM_ERRO = 'Moeda inválida'
    WHERE MOE NOT IN ('BRL', 'USD', 'EUR')
      AND C_ID_UPLOAD = @ID_UPLOAD;

    UPDATE STG_BALG
    SET FLAG_ERRO = 1,
        MENSAGEM_ERRO = COALESCE(MENSAGEM_ERRO + ' | ', '') + 'Saldo inválido'
    WHERE (SLD IS NULL OR SLD < 0)
      AND C_ID_UPLOAD = @ID_UPLOAD;

    INSERT INTO dbo.T_LOG_PROCESSAMENTO (ID_UPLOAD, ETAPA, STATUS, MENSAGEM)
    VALUES (@ID_UPLOAD, 'VALIDACAO', 'SUCESSO', 'Validação executada com sucesso.');
END
GO

-- 3. PROC_GERA_LOG_BALG - Registrar erros encontrados
IF OBJECT_ID('dbo.PROC_GERA_LOG_BALG', 'P') IS NOT NULL DROP PROCEDURE dbo.PROC_GERA_LOG_BALG;
GO

CREATE PROCEDURE dbo.PROC_GERA_LOG_BALG
    @ID_UPLOAD INT
AS
BEGIN
    SET NOCOUNT ON;

    INSERT INTO dbo.T_LOG_PROCESSAMENTO (ID_UPLOAD, ETAPA, STATUS, MENSAGEM)
    SELECT
        @ID_UPLOAD,
        'VALIDACAO_ERROS',
        'ERRO',
        CONCAT('Conta: ', CD_CONTA, ' - ', MENSAGEM_ERRO)
    FROM STG_BALG
    WHERE C_ID_UPLOAD = @ID_UPLOAD AND FLAG_ERRO = 1;
END
GO

-- 4. PROC_APLICA_BALG - Grava dados válidos na base analítica
IF OBJECT_ID('dbo.PROC_APLICA_BALG', 'P') IS NOT NULL DROP PROCEDURE dbo.PROC_APLICA_BALG;
GO

CREATE PROCEDURE dbo.PROC_APLICA_BALG
    @ID_UPLOAD INT
AS
BEGIN
    SET NOCOUNT ON;

    INSERT INTO dbo.T_PCONP_BASE_CONSL_ANLTCA (NOME_ARQUIVO, CD_EMP, CD_CONTA, PRZ, MOE, SLD, D_BASE)
    SELECT NOME_ARQUIVO, CD_EMP, CD_CONTA, PRZ, MOE, SLD, D_BASE
    FROM dbo.STG_BALG
    WHERE C_ID_UPLOAD = @ID_UPLOAD AND FLAG_ERRO = 0;

    INSERT INTO dbo.T_LOG_PROCESSAMENTO (ID_UPLOAD, ETAPA, STATUS, MENSAGEM)
    VALUES (@ID_UPLOAD, 'APLICACAO', 'SUCESSO', 'Dados válidos aplicados com sucesso.');
END
GO

-- 5. PROC_ATUALIZA_CTRL_BALG - Atualiza status de execução
IF OBJECT_ID('dbo.PROC_ATUALIZA_CTRL_BALG', 'P') IS NOT NULL DROP PROCEDURE dbo.PROC_ATUALIZA_CTRL_BALG;
GO

CREATE PROCEDURE dbo.PROC_ATUALIZA_CTRL_BALG
    @ID_UPLOAD INT,
    @STATUS VARCHAR(50),
    @MENSAGEM VARCHAR(MAX) = NULL
AS
BEGIN
    SET NOCOUNT ON;

    UPDATE T_CTRL_UPLOAD_BALG
    SET STATUS = @STATUS,
        MENSAGEM = @MENSAGEM,
        DT_FIM = CASE WHEN @STATUS IN ('FINALIZADO', 'ERRO') THEN GETDATE() ELSE NULL END
    WHERE ID_UPLOAD = @ID_UPLOAD;

    INSERT INTO dbo.T_LOG_PROCESSAMENTO (ID_UPLOAD, ETAPA, STATUS, MENSAGEM)
    VALUES (@ID_UPLOAD, 'ATUALIZA_CTRL', @STATUS, ISNULL(@MENSAGEM, 'Status atualizado.'));
END
GO

-------------------------------------------------------------
-- 🏁 Orquestrador Final (Pipeline Completo)
-------------------------------------------------------------
IF OBJECT_ID('dbo.PROC_PROCESSA_BALG', 'P') IS NOT NULL DROP PROCEDURE dbo.PROC_PROCESSA_BALG;
GO

CREATE PROCEDURE dbo.PROC_PROCESSA_BALG
    @ID_UPLOAD INT,
    @NOME_ARQUIVO VARCHAR(255)
AS
BEGIN
    BEGIN TRY
        EXEC dbo.PROC_ATUALIZA_CTRL_BALG @ID_UPLOAD, 'PROCESSANDO_SQL';

        EXEC dbo.PROC_CARGA_BALG @ID_UPLOAD, @NOME_ARQUIVO;
        EXEC dbo.PROC_VALIDA_BALG @ID_UPLOAD;
        EXEC dbo.PROC_GERA_LOG_BALG @ID_UPLOAD;
        EXEC dbo.PROC_APLICA_BALG @ID_UPLOAD;

        EXEC dbo.PROC_ATUALIZA_CTRL_BALG @ID_UPLOAD, 'FINALIZADO';
    END TRY
    BEGIN CATCH
        DECLARE @ERROR_MSG NVARCHAR(MAX) = ERROR_MESSAGE();
        EXEC dbo.PROC_ATUALIZA_CTRL_BALG @ID_UPLOAD, 'ERRO', @ERROR_MSG;
        THROW;
    END CATCH
END
GO

/*************************************************************
 * 🔥 Cenário de Teste Completo
 *************************************************************/
-- Inserir controle de upload
INSERT INTO dbo.T_CTRL_UPLOAD_BALG (NOME_ARQUIVO, STATUS)
VALUES ('BALG_TESTE.csv', 'UPLOADING');
DECLARE @ID_UPLOAD INT = SCOPE_IDENTITY();

-- Inserir dados simulados na STG_BALG
INSERT INTO dbo.STG_BALG (C_ID_UPLOAD, NOME_ARQUIVO, D_BASE, CD_EMP, CD_CONTA, PRZ, MOE, SLD)
VALUES 
(@ID_UPLOAD, 'BALG_TESTE.csv', '2025-06-01', '001', '123456', '30', 'BRL', 1000),
(@ID_UPLOAD, 'BALG_TESTE.csv', '2025-06-01', '001', '123457', '60', 'USD', 2000),
(@ID_UPLOAD, 'BALG_TESTE.csv', '2025-06-01', '001', '123458', '90', 'INVALIDO', 3000);

-- Executar processamento completo
EXEC dbo.PROC_PROCESSA_BALG @ID_UPLOAD, 'BALG_TESTE.csv';

-- Consultar resultados
SELECT * FROM dbo.T_CTRL_UPLOAD_BALG;
SELECT * FROM dbo.T_LOG_PROCESSAMENTO;
SELECT * FROM dbo.STG_BALG;
SELECT * FROM dbo.T_PCONP_BASE_CONSL_ANLTCA;

/*************************************************************
 * 🏗️ ESTRATÉGIA DE PARTICIONAMENTO - SQL SERVER 2019
 * Tabela: T_PCONP_BASE_CONSL_ANLTCA
 * Particionamento por: D_BASE (Data da Base)
 *************************************************************/

---------------------------------------------------------------
-- 1️⃣ Função de Particionamento (por Ano)
---------------------------------------------------------------
IF EXISTS (SELECT * FROM sys.partition_functions WHERE name = 'pfDataBase')
    DROP PARTITION FUNCTION pfDataBase;
GO

CREATE PARTITION FUNCTION pfDataBase (DATE)
AS RANGE LEFT FOR VALUES 
(
    '2022-12-31',
    '2023-12-31',
    '2024-12-31',
    '2025-12-31',
    '2026-12-31'
);
GO

---------------------------------------------------------------
-- 2️⃣ Esquema de Particionamento
---------------------------------------------------------------
IF EXISTS (SELECT * FROM sys.partition_schemes WHERE name = 'psDataBase')
    DROP PARTITION SCHEME psDataBase;
GO

CREATE PARTITION SCHEME psDataBase
AS PARTITION pfDataBase
ALL TO ([PRIMARY]);
GO

---------------------------------------------------------------
-- 3️⃣ Tabela Destino com Particionamento
---------------------------------------------------------------
IF OBJECT_ID('dbo.T_PCONP_BASE_CONSL_ANLTCA', 'U') IS NOT NULL
    DROP TABLE dbo.T_PCONP_BASE_CONSL_ANLTCA;
GO

CREATE TABLE dbo.T_PCONP_BASE_CONSL_ANLTCA (
    NOME_ARQUIVO VARCHAR(255),
    CD_EMP VARCHAR(20),
    CD_CONTA VARCHAR(50),
    PRZ VARCHAR(10),
    MOE VARCHAR(10),
    SLD DECIMAL(25,2),
    D_BASE DATE NOT NULL
)
ON psDataBase (D_BASE);
GO

---------------------------------------------------------------
-- 🔍 4️⃣ Verificar Mapeamento das Partições
---------------------------------------------------------------
SELECT
    ps.name AS PartitionScheme,
    pf.name AS PartitionFunction,
    p.partition_number,
    prv.value AS RangeBoundary,
    p.rows AS RowsInPartition
FROM sys.indexes i
INNER JOIN sys.partition_schemes ps ON i.data_space_id = ps.data_space_id
INNER JOIN sys.partition_functions pf ON ps.function_id = pf.function_id
INNER JOIN sys.partitions p ON i.object_id = p.object_id AND i.index_id <= 1
LEFT JOIN sys.partition_range_values prv ON pf.function_id = prv.function_id AND p.partition_number = prv.boundary_id + 1
WHERE i.object_id = OBJECT_ID('dbo.T_PCONP_BASE_CONSL_ANLTCA')
ORDER BY p.partition_number;
GO

CREATE PROCEDURE dbo.SP_GERA_PARTICAO
    @DataLimite DATE
AS
BEGIN
    SET NOCOUNT ON;

    IF NOT EXISTS (
        SELECT 1 
        FROM sys.partition_range_values prv
        INNER JOIN sys.partition_functions pf ON prv.function_id = pf.function_id
        WHERE pf.name = 'pfDataBase' AND prv.value = @DataLimite
    )
    BEGIN
        PRINT 'Criando nova partição para ' + CAST(@DataLimite AS VARCHAR(20));
        ALTER PARTITION FUNCTION pfDataBase()
        SPLIT RANGE (@DataLimite);
    END
    ELSE
    BEGIN
        PRINT 'Partição para ' + CAST(@DataLimite AS VARCHAR(20)) + ' já existe.';
    END
END;
GO

CREATE PROCEDURE dbo.SP_REMOVE_PARTICAO
    @DataLimite DATE
AS
BEGIN
    SET NOCOUNT ON;

    IF EXISTS (
        SELECT 1 
        FROM sys.partition_range_values prv
        INNER JOIN sys.partition_functions pf ON prv.function_id = pf.function_id
        WHERE pf.name = 'pfDataBase' AND prv.value = @DataLimite
    )
    BEGIN
        PRINT 'Removendo partição para ' + CAST(@DataLimite AS VARCHAR(20));
        ALTER PARTITION FUNCTION pfDataBase()
        MERGE RANGE (@DataLimite);
    END
    ELSE
    BEGIN
        PRINT 'Partição para ' + CAST(@DataLimite AS VARCHAR(20)) + ' não existe.';
    END
END;
GO

CREATE OR ALTER PROCEDURE PROC_PROCESSA_BALG_TEMP
    @ID_UPLOAD int,
    @NOME_ARQUIVO varchar(255),
    @TEMP_TABLE varchar(128)
AS
BEGIN
    SET NOCOUNT ON;
    
    BEGIN TRY
        -- Insere na tabela final apenas os dados deste arquivo
        INSERT INTO STG_BALG (
            C_ID_UPLOAD, NOME_ARQUIVO, D_BASE, CD_EMP, CD_CONTA, 
            PRZ, MOE, SLD, FLAG_ERRO, MENSAGEM_ERRO, DT_IMPORTACAO
        )
        SELECT 
            C_ID_UPLOAD, NOME_ARQUIVO, D_BASE, CD_EMP, CD_CONTA, 
            PRZ, MOE, SLD, FLAG_ERRO, MENSAGEM_ERRO, DT_IMPORTACAO
        FROM #STG_BALG_@ID_UPLOAD_@TEMP_TABLE;

        -- Atualiza o status do upload
        UPDATE UPLOAD_ARQUIVOS 
        SET STATUS = 'FINALIZADO',
            DT_FIM = GETDATE()
        WHERE ID_UPLOAD = @ID_UPLOAD 
        AND NOME_ARQUIVO = @NOME_ARQUIVO;

    END TRY
    BEGIN CATCH
        -- Em caso de erro, marca o upload como com erro
        UPDATE UPLOAD_ARQUIVOS 
        SET STATUS = 'ERRO',
            DT_FIM = GETDATE(),
            MENSAGEM_ERRO = ERROR_MESSAGE()
        WHERE ID_UPLOAD = @ID_UPLOAD 
        AND NOME_ARQUIVO = @NOME_ARQUIVO;

        THROW;
    END CATCH
END
