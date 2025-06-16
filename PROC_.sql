CREATE PROCEDURE [dbo].[PROC_IMP_ARQ_PLCC_WORKF]
    @C_ID_USUAR   INT,
    @C_ID_GERAL   BIGINT,
    @R_NOME_ARQ   VARCHAR(100),
    @STATUSWORK   INT,
    @VERSAO       INT
AS
BEGIN
    SET NOCOUNT ON;
    
    -- Configurações de otimização para SQL Server 2019
    SET TRANSACTION ISOLATION LEVEL READ COMMITTED;
    SET LOCK_TIMEOUT 30000; -- 30 segundos
    SET ARITHABORT ON;
    SET CONCAT_NULL_YIELDS_NULL ON;
    SET ANSI_NULLS ON;
    SET ANSI_PADDING ON;
    SET ANSI_WARNINGS ON;
    SET QUOTED_IDENTIFIER ON;

    -- Configurar memória para operações em lote
    DECLARE @BatchSize INT = 10000;
    DECLARE @TotalRows INT;
    DECLARE @ProcessedRows INT = 0;

    BEGIN TRY
        -- Limpar tabelas temporárias caso existam
        IF OBJECT_ID('tempdb..#TMP_ARQ_PLCC') IS NOT NULL DROP TABLE #TMP_ARQ_PLCC;
        IF OBJECT_ID('tempdb..#TMP_CTA_INTRN') IS NOT NULL DROP TABLE #TMP_CTA_INTRN;
        IF OBJECT_ID('tempdb..#TMP_DEPARA_CTA') IS NOT NULL DROP TABLE #TMP_DEPARA_CTA;

        BEGIN TRANSACTION;

        -- 1) Validar formato do nome do arquivo e extrair código da empresa
        DECLARE @COD_EMPR VARCHAR(3);
        DECLARE @C_ID_PLANO_CTA_EMPR INT;
        
        -- Validar formato do arquivo (XXX_PLCC_YYYYMM.CSV)
        IF @R_NOME_ARQ NOT LIKE '___PLCC_%[0-9][0-9][0-9][0-9].CSV'
        BEGIN
            RAISERROR('Formato do nome do arquivo inválido. Deve ser XXX_PLCC_YYYYMM.CSV', 16, 1);
        END

        -- Extrair código da empresa
        SET @COD_EMPR = LEFT(@R_NOME_ARQ, 3);

        -- 2) Validar existência da empresa e obter seu plano de contas
        SELECT @C_ID_PLANO_CTA_EMPR = C_ID_PLANO_CTA
        FROM TB_EMPRESA WITH (NOLOCK)
        WHERE COD_EMPR = @COD_EMPR;

        IF @C_ID_PLANO_CTA_EMPR IS NULL
        BEGIN
            RAISERROR('Empresa %s não encontrada em TB_EMPRESA', 16, 1, @COD_EMPR);
        END

        -- 3) Criar tabelas temporárias com índices
        -- Tabela temporária para dados do arquivo
        CREATE TABLE #TMP_ARQ_PLCC (
            C_RZ_CTA VARCHAR(4),
            C_NRO_CTA VARCHAR(20),
            R_DESCR_CTA VARCHAR(100),
            N_FL_AJUST INT,
            R_CHAVE_CTA VARCHAR(50),
            C_ID_PLANO_CTA INT,
            INDCD_REG_ATULZ INT,
            U_STTUS BIT,
            D_DATA_BASE DATE,
            PLANO_CTA VARCHAR(10),
            PLANO_CTA_CONSLD VARCHAR(10),
            C_NRO_CTA_CONSLD VARCHAR(20)
        );

        -- Tabela temporária para contas internas
        CREATE TABLE #TMP_CTA_INTRN (
            C_RZ_CTA VARCHAR(4),
            C_NRO_CTA VARCHAR(20),
            R_DESCR_CTA VARCHAR(100),
            N_FL_AJUST INT,
            R_CHAVE_CTA VARCHAR(50),
            C_ID_PLANO_CTA INT,
            INDCD_REG_ATULZ INT,
            U_STTUS BIT,
            D_DATA_BASE DATE
        );

        -- Tabela temporária para de/para
        CREATE TABLE #TMP_DEPARA_CTA (
            R_CHAVE_CTA VARCHAR(50),
            D_CHAVE_CTA_CONSLD VARCHAR(50),
            D_DATA_BASE DATE,
            U_STTUS BIT
        );

        -- Criar índices nas tabelas temporárias
        CREATE NONCLUSTERED INDEX IX_TMP_ARQ_PLCC_CHAVE ON #TMP_ARQ_PLCC (R_CHAVE_CTA, C_ID_PLANO_CTA, D_DATA_BASE)
        INCLUDE (C_RZ_CTA, C_NRO_CTA, R_DESCR_CTA, N_FL_AJUST, INDCD_REG_ATULZ, U_STTUS);

        CREATE NONCLUSTERED INDEX IX_TMP_CTA_INTRN_CHAVE ON #TMP_CTA_INTRN (R_CHAVE_CTA, C_ID_PLANO_CTA, D_DATA_BASE)
        INCLUDE (C_RZ_CTA, C_NRO_CTA, R_DESCR_CTA, N_FL_AJUST, INDCD_REG_ATULZ, U_STTUS);

        CREATE NONCLUSTERED INDEX IX_TMP_DEPARA_CHAVE ON #TMP_DEPARA_CTA (R_CHAVE_CTA, D_CHAVE_CTA_CONSLD, D_DATA_BASE)
        INCLUDE (U_STTUS);

        -- 4) Carregar dados do arquivo na tabela temporária em lotes
        INSERT INTO #TMP_ARQ_PLCC
        SELECT 
            CASE WHEN LEN(C_NRO_CTA) > 4 THEN '-1' ELSE RIGHT('0000' + C_NRO_CTA, 4) END,
            CASE WHEN LEN(C_NRO_CTA) <= 4 THEN '-1' ELSE C_NRO_CTA END,
            R_DESCR_CTA,
            0,
            PLANO_CTA + '#' + 
                CASE WHEN LEN(C_NRO_CTA) > 4 
                     THEN C_NRO_CTA 
                     ELSE RIGHT('0000' + C_NRO_CTA, 4) 
                END,
            C_ID_PLANO_CTA,
            1,
            CASE WHEN U_STTUS = 'I' THEN 0 ELSE 1 END,
            D_DATA_BASE,
            PLANO_CTA,
            PLANO_CTA_CONSLD,
            C_NRO_CTA_CONSLD
        FROM TXIMP_ARQ_PLCC WITH (NOLOCK)
        WHERE C_ID_GERAL = @C_ID_GERAL;

        -- 5) Validar consistência do plano de contas
        IF EXISTS (
            SELECT 1 
            FROM #TMP_ARQ_PLCC T
            INNER JOIN TB_PLANO_PC P WITH (NOLOCK) ON T.PLANO_CTA = P.PLANO_CTA
            WHERE P.C_ID_PLANO_CTA <> @C_ID_PLANO_CTA_EMPR
        )
        BEGIN
            RAISERROR('Arquivo contém planos de contas não associados à empresa %s', 16, 1, @COD_EMPR);
        END

        -- 6) Determinar a data base da carga
        DECLARE @D_DATA_BASE DATE;
        SELECT TOP 1 @D_DATA_BASE = D_DATA_BASE
        FROM #TMP_ARQ_PLCC;

        -- 7) Verificar se a data base está ativa
        IF NOT EXISTS (
            SELECT 1 FROM T_DATA_BASE WITH (NOLOCK)
            WHERE D_DATA_BASE = @D_DATA_BASE AND U_STTUS = 1
        )
        BEGIN
            RAISERROR('Data Base %s não está ativa em T_DATA_BASE.', 16, 1, @D_DATA_BASE);
        END

        -- 8) Carregar contas internas existentes em lotes
        INSERT INTO #TMP_CTA_INTRN
        SELECT 
            C_RZ_CTA,
            C_NRO_CTA,
            R_DESCR_CTA,
            N_FL_AJUST,
            R_CHAVE_CTA,
            C_ID_PLANO_CTA,
            INDCD_REG_ATULZ,
            U_STTUS,
            D_DATA_BASE
        FROM T_CTA_INTRN WITH (NOLOCK)
        WHERE D_DATA_BASE = @D_DATA_BASE
          AND C_ID_PLANO_CTA = @C_ID_PLANO_CTA_EMPR;

        -- 9) Processar contas internas (T_CTA_INTRN) em lotes
        SELECT @TotalRows = COUNT(*) FROM #TMP_ARQ_PLCC;

        WHILE @ProcessedRows < @TotalRows
        BEGIN
            MERGE T_CTA_INTRN AS T
            USING (
                SELECT TOP (@BatchSize)
                    C_RZ_CTA,
                    C_NRO_CTA,
                    R_DESCR_CTA,
                    N_FL_AJUST,
                    R_CHAVE_CTA,
                    C_ID_PLANO_CTA,
                    INDCD_REG_ATULZ,
                    U_STTUS,
                    D_DATA_BASE
                FROM #TMP_ARQ_PLCC
                ORDER BY R_CHAVE_CTA
                OFFSET @ProcessedRows ROWS
            ) AS S
            ON T.R_CHAVE_CTA = S.R_CHAVE_CTA 
               AND T.C_ID_PLANO_CTA = S.C_ID_PLANO_CTA
               AND T.D_DATA_BASE = S.D_DATA_BASE
            WHEN MATCHED THEN
                UPDATE SET 
                    T.C_RZ_CTA = S.C_RZ_CTA,
                    T.C_NRO_CTA = S.C_NRO_CTA,
                    T.R_DESCR_CTA = S.R_DESCR_CTA,
                    T.N_FL_AJUST = S.N_FL_AJUST,
                    T.INDCD_REG_ATULZ = S.INDCD_REG_ATULZ,
                    T.U_STTUS = S.U_STTUS
            WHEN NOT MATCHED THEN
                INSERT (C_RZ_CTA, C_NRO_CTA, R_DESCR_CTA, N_FL_AJUST, R_CHAVE_CTA, 
                        C_ID_PLANO_CTA, INDCD_REG_ATULZ, U_STTUS, D_DATA_BASE)
                VALUES (S.C_RZ_CTA, S.C_NRO_CTA, S.R_DESCR_CTA, S.N_FL_AJUST, S.R_CHAVE_CTA,
                        S.C_ID_PLANO_CTA, S.INDCD_REG_ATULZ, S.U_STTUS, S.D_DATA_BASE);

            SET @ProcessedRows = @ProcessedRows + @BatchSize;
        END

        -- 10) Carregar de/para existentes
        INSERT INTO #TMP_DEPARA_CTA
        SELECT 
            R_CHAVE_CTA,
            D_CHAVE_CTA_CONSLD,
            D_DATA_BASE,
            U_STTUS
        FROM T_DEPARA_CTA WITH (NOLOCK)
        WHERE D_DATA_BASE = @D_DATA_BASE;

        -- 11) Processar de/para (T_DEPARA_CTA) em lotes
        SET @ProcessedRows = 0;
        SELECT @TotalRows = COUNT(*) 
        FROM #TMP_ARQ_PLCC S
        INNER JOIN T_CTA_CONSLD C WITH (NOLOCK) ON 
            C.D_CHAVE_CTA_CONSLD = PLANO_CTA_CONSLD + '#' + C_NRO_CTA_CONSLD
            AND C.D_DATA_BASE = @D_DATA_BASE
            AND C.U_STTUS = 1
        WHERE S.C_NRO_CTA_CONSLD IS NOT NULL;

        WHILE @ProcessedRows < @TotalRows
        BEGIN
            MERGE T_DEPARA_CTA AS T
            USING (
                SELECT TOP (@BatchSize)
                    R_CHAVE_CTA,
                    PLANO_CTA_CONSLD + '#' + C_NRO_CTA_CONSLD AS D_CHAVE_CTA_CONSLD,
                    D_DATA_BASE,
                    U_STTUS
                FROM #TMP_ARQ_PLCC S
                INNER JOIN T_CTA_CONSLD C WITH (NOLOCK) ON 
                    C.D_CHAVE_CTA_CONSLD = PLANO_CTA_CONSLD + '#' + C_NRO_CTA_CONSLD
                    AND C.D_DATA_BASE = @D_DATA_BASE
                    AND C.U_STTUS = 1
                WHERE S.C_NRO_CTA_CONSLD IS NOT NULL
                ORDER BY R_CHAVE_CTA
                OFFSET @ProcessedRows ROWS
            ) AS S
            ON T.R_CHAVE_CTA = S.R_CHAVE_CTA
               AND T.D_CHAVE_CTA_CONSLD = S.D_CHAVE_CTA_CONSLD
               AND T.D_DATA_BASE = S.D_DATA_BASE
            WHEN MATCHED THEN
                UPDATE SET T.U_STTUS = S.U_STTUS
            WHEN NOT MATCHED THEN
                INSERT (R_CHAVE_CTA, D_CHAVE_CTA_CONSLD, D_DATA_BASE, U_STTUS)
                VALUES (S.R_CHAVE_CTA, S.D_CHAVE_CTA_CONSLD, S.D_DATA_BASE, S.U_STTUS);

            SET @ProcessedRows = @ProcessedRows + @BatchSize;
        END

        -- 12) Atualizar status do monitor
        UPDATE T_MONITOR_PL_CONTA
        SET C_ID_STTUS_WORK = @STATUSWORK
        WHERE C_ID_PLANO_CTA = @C_ID_PLANO_CTA_EMPR
          AND D_DATA_BASE = @D_DATA_BASE
          AND R_NOME_ARQ = @R_NOME_ARQ;

        -- 13) Limpar tabelas temporárias
        IF OBJECT_ID('tempdb..#TMP_ARQ_PLCC') IS NOT NULL DROP TABLE #TMP_ARQ_PLCC;
        IF OBJECT_ID('tempdb..#TMP_CTA_INTRN') IS NOT NULL DROP TABLE #TMP_CTA_INTRN;
        IF OBJECT_ID('tempdb..#TMP_DEPARA_CTA') IS NOT NULL DROP TABLE #TMP_DEPARA_CTA;

        COMMIT TRANSACTION;
    END TRY
    BEGIN CATCH
        -- Limpar tabelas temporárias em caso de erro
        IF OBJECT_ID('tempdb..#TMP_ARQ_PLCC') IS NOT NULL DROP TABLE #TMP_ARQ_PLCC;
        IF OBJECT_ID('tempdb..#TMP_CTA_INTRN') IS NOT NULL DROP TABLE #TMP_CTA_INTRN;
        IF OBJECT_ID('tempdb..#TMP_DEPARA_CTA') IS NOT NULL DROP TABLE #TMP_DEPARA_CTA;

        IF XACT_STATE() <> 0
            ROLLBACK TRANSACTION;

        DECLARE @ErrMsg NVARCHAR(4000) = ERROR_MESSAGE();
        DECLARE @ErrSev INT = ERROR_SEVERITY();
        DECLARE @ErrLine INT = ERROR_LINE();
        
        RAISERROR('Erro na PROC_IMP_ARQ_PLCC_WORKF: %s (Sev %d, Line %d)',
                 @ErrSev, 1, @ErrMsg, @ErrSev, @ErrLine);
    END CATCH;
END
GO