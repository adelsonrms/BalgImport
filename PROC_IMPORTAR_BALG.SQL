GO
ALTER PROCEDURE [dbo].[PROC_INSC_BALG_ARRAY]    @C_ID_ARQ_CTRL_PROCM VARCHAR(MAX)="0" AS
BEGIN
 
    BEGIN TRY
       
        BEGIN TRANSACTION
 
 
        --DECLARE @C_ID_ARQ_CTRL_PROCM  VARCHAR(max)="131943"
        DECLARE @D_BASES                VARCHAR(200)=""
        DECLARE @BANCO_LEITURA          VARCHAR(200)=dbo.FUNC_RETORNA_BANCO_LEITURA()
        DECLARE @BANCO_GRAVA            VARCHAR(200)=dbo.FUNC_RETORNA_BANCO_GRAVACAO()
        DECLARE @STR_SQL                VARCHAR(MAX)=""
        DECLARE @D_BASE                 VARCHAR(20)
        DECLARE @ARRAY_ERRO             VARCHAR(MAX)=""
        DECLARE @ARRAY_OK               VARCHAR(MAX)=""
        DECLARE @ARRAY_ARQ_ERRO         VARCHAR(MAX) = ""
       
        PRINT '### => DADOS DO ARQUIVO CSV....'
 
        IF OBJECT_ID(N'tempdb..#T_ARQ_CTRL_PROCM', N'U') IS NOT NULL  
        DROP TABLE #T_ARQ_CTRL_PROCM;
 
        CREATE TABLE #T_ARQ_CTRL_PROCM(ID INT IDENTITY(1,1),C_ID_ARQ_CTRL_PROCM INT,C_ID_CTRL_PROCM INT,C_ID_TPO_LYOUT INT,R_NOME_ARQ VARCHAR(100),R_DADOS VARCHAR(MAX),C_COD_ERRO INT,R_NOME_ARQ_SAS VARCHAR(100),D_BASE DATE)    
        SET @STR_SQL ="
        INSERT INTO #T_ARQ_CTRL_PROCM(C_ID_ARQ_CTRL_PROCM,C_ID_CTRL_PROCM,C_ID_TPO_LYOUT,R_NOME_ARQ,R_DADOS,C_COD_ERRO,D_BASE)
        SELECT A.C_ID_ARQ_CTRL_PROCM
                ,A.C_ID_CTRL_PROCM
                ,A.C_ID_TPO_LYOUT
                ,A.R_NOME_ARQ
                ,A.R_DADOS
                ,B.C_COD_ERRO
                ,dbo.GETS_NU_BASE_EM_D_BASE(LEFT(RIGHT(A.R_NOME_ARQ,10),6))
        FROM " + @BANCO_LEITURA + "T_ARQ_CTRL_PROCM AS A
        LEFT JOIN " + @BANCO_LEITURA + "T_CTRL_PROCM AS B ON A.R_NOME_ARQ = B.R_NOME_ARQ
        WHERE A.C_ID_TPO_LYOUT = 200
        AND A.C_ID_CTRL_PROCM IN (" + @C_ID_ARQ_CTRL_PROCM + ")
        AND B.C_COD_ERRO = '101'
        ORDER BY A.C_ID_ARQ_CTRL_PROCM"
        print @STR_SQL
        EXEC (@STR_SQL)
       
        DECLARE @R_NOME_ARQ VARCHAR(500)=""
        DECLARE @R_NOME_ARQUIVOS VARCHAR(MAX)=""
        DECLARE CURSOR_NOME_ARQ CURSOR FOR
        SELECT DISTINCT R_NOME_ARQ FROM #T_ARQ_CTRL_PROCM
        OPEN CURSOR_NOME_ARQ
        FETCH NEXT FROM CURSOR_NOME_ARQ INTO @R_NOME_ARQ
        WHILE @@FETCH_STATUS = 0 BEGIN
            SET @R_NOME_ARQUIVOS=@R_NOME_ARQUIVOS + "'" + @R_NOME_ARQ + "',"
            FETCH NEXT FROM CURSOR_NOME_ARQ INTO @R_NOME_ARQ
        END
        SET @R_NOME_ARQUIVOS=SUBSTRING(@R_NOME_ARQUIVOS,1,LEN(@R_NOME_ARQUIVOS)-1)
 
        --RETURN
 
        CLOSE CURSOR_NOME_ARQ
        DEALLOCATE CURSOR_NOME_ARQ
 
        PRINT '### => LIMPOU DADOS ATUAIS....'
 
        --EXCLUINDO DADOS DO MESMO ARQUIVO NA TABELA ANALÍTICA E LOGGER
        SET @STR_SQL="
        DELETE FROM "+ @BANCO_GRAVA +"T_PCONP_BASE_CONSL_ANLTCA WHERE N_COD_ORIGE IN ('102', '111', '112', '191', '192') AND R_NOME_ARQ IN (" + @R_NOME_ARQUIVOS + ")"
        EXEC (@STR_SQL)
 
        SET @STR_SQL="
        DELETE FROM " + @BANCO_GRAVA + "T_PCONP_BASE_CONSL_ANLTCA_LOGERR WHERE N_COD_ORIGE IN ('102', '111', '112', '191', '192') AND R_NOME_ARQ IN (" + @R_NOME_ARQUIVOS + ")"
        EXEC (@STR_SQL)
 
        PRINT '### => DETERMINA DADA BASE DO NOME DO ARQUIVO....'
 
        IF ((SELECT COUNT(*) FROM #T_ARQ_CTRL_PROCM)<>0) BEGIN
            DECLARE CURSOR_D_BASE CURSOR FOR
            SELECT DISTINCT LEFT(RIGHT(R_NOME_ARQ,10),6)
            FROM #T_ARQ_CTRL_PROCM
            OPEN CURSOR_D_BASE
            FETCH NEXT FROM CURSOR_D_BASE INTO @D_BASE
            WHILE @@FETCH_STATUS = 0 BEGIN
                SET @D_BASES=@D_BASES + "'" + CAST(dbo.GETS_NU_BASE_EM_D_BASE(@D_BASE) AS VARCHAR) + "',"
                FETCH NEXT FROM CURSOR_D_BASE INTO @D_BASE
            END
            SET @D_BASES=SUBSTRING(@D_BASES,1,LEN(@D_BASES)-1)
 
            CLOSE CURSOR_D_BASE
            DEALLOCATE CURSOR_D_BASE
 
            DECLARE @STR_D_DATA_BASE VARCHAR(MAX) = CONCAT(@D_BASE, '01')
 
            print @D_BASES
 
            DECLARE @D_DATA_BASE DATE = CAST(@STR_D_DATA_BASE AS DATE)
 
            PRINT 'DATA BASE STR'
            PRINT @STR_D_DATA_BASE
           
            PRINT 'DATA BASE DT'
            PRINT @D_DATA_BASE
 
            UPDATE #T_ARQ_CTRL_PROCM SET R_NOME_ARQ_SAS=SUBSTRING(R_NOME_ARQ,1,dbo.GETS_BUSCA_POS_CHAR(R_NOME_ARQ,'.')-1)
 
            PRINT '### => DADA BASE DO NOME DO ARQUIVO....'  + @STR_D_DATA_BASE
            PRINT '### => DEPARA DE EMPRESAS '
 
            IF OBJECT_ID(N'tempdb..#T_DEPARA_EMPR', N'U') IS NOT NULL  
            DROP TABLE #T_DEPARA_EMPR;
 
            CREATE TABLE #T_DEPARA_EMPR(DE_C_COD_EMPR VARCHAR (100),PARA_C_COD_EMPR VARCHAR (100))
            SET @STR_SQL="
            INSERT INTO #T_DEPARA_EMPR(DE_C_COD_EMPR,PARA_C_COD_EMPR)
            SELECT DE_C_COD_EMPR,PARA_C_COD_EMPR
            FROM " + @BANCO_LEITURA + "T_DEPARA_EMPR"
            EXEC (@STR_SQL)
 
            IF OBJECT_ID(N'tempdb..#TBALG', N'U') IS NOT NULL  
            DROP TABLE #TBALG;
 
            PRINT '### => MONTA TABELA DE DADOS #TBALG'
 
            CREATE TABLE #TBALG(LIN INT,C_ID_ARQ_CTRL_PROCM INT,D_BASE VARCHAR(100),CD_EMP VARCHAR(100),CD_CONTA VARCHAR(100),PRZ VARCHAR(100),MOE VARCHAR(100),SLD DECIMAL(25,2),C_PL_EMP VARCHAR(100),CH_FECHAMENTO INT,STS_EMPR INT,FLG_EMP INT,CD_CHV_CTA VARCHAR(100),CD_COSIF VARCHAR(100),CD_RAZAO VARCHAR(100),DS_ORIGEM VARCHAR(100),R_NOME_ARQ VARCHAR(300),C_ID_CTRL_PROCM INT,FLG_EMP_VS_LAY BIT,FLG_NTCAO_CTFCA BIT,FLG_DT_PROC_ARQ BIT,FLG_PCTA BIT,FLG_SLD_ZRO BIT,FLG_CTA_AJU BIT, FLG_DT_IGUAL BIT)
 
            -- ARMS, 05/05/2025 - Extrai o conteudo do arquivo separando as colunas por ;
            INSERT INTO #TBALG(LIN,C_ID_ARQ_CTRL_PROCM,D_BASE,CD_EMP,CD_CONTA,PRZ,MOE,SLD,R_NOME_ARQ,C_ID_CTRL_PROCM)
            SELECT
                ID,
                C_ID_ARQ_CTRL_PROCM,
                dbo.GETS_NU_BASE_EM_D_BASE(RIGHT(CAST([COLUNA 1] AS VARCHAR),4)+ LEFT(RIGHT('0'+CAST([COLUNA 1] AS VARCHAR),6),2)),
                ISNULL(EMP.PARA_C_COD_EMPR,PVT.[COLUNA 2]),
                CAST([COLUNA 3] AS VARCHAR),
                CAST([COLUNA 4] AS VARCHAR),
                CAST([COLUNA 5] AS VARCHAR),
                -- saldo V_SDO
                CASE
                    WHEN ISNUMERIC([COLUNA 6])=1 THEN
                        CAST(REPLACE([COLUNA 6],',','.')AS DECIMAL(25,2))
                ELSE
                    0
                END ,
                R_NOME_ARQ,
                C_ID_CTRL_PROCM
            FROM
                (
            SELECT
                ID,
                R_DADOS,
                C_ID_ARQ_CTRL_PROCM,
                C_ID_CTRL_PROCM,
                'COLUNA ' + CAST(ROW_NUMBER() OVER(PARTITION BY ID ORDER BY ID) AS VARCHAR) AS COL,
                SPLIT.value,ARQ.R_NOME_ARQ FROM #T_ARQ_CTRL_PROCM AS ARQ CROSS APPLY STRING_SPLIT(R_DADOS,';') AS SPLIT) AS TBL PIVOT
                (MAX(VALUE) FOR COL IN (
                    [COLUNA 1],
                    [COLUNA 2],
                    [COLUNA 3],
                    [COLUNA 4],
                    [COLUNA 5],
                    [COLUNA 6])
                ) AS PVT
                LEFT JOIN #T_DEPARA_EMPR EMP ON CAST(PVT.[COLUNA 2] AS VARCHAR)=EMP.DE_C_COD_EMPR
            WHERE
                CAST([COLUNA 4] AS VARCHAR) NOT IN ('Prazo','CA_PRAZO') AND TRIM(CAST([COLUNA 2] AS VARCHAR)) NOT IN('0COMPANY')
 
            PRINT '### => LIMPA LANÇAMENTOS ZERADO'
 
            DELETE #TBALG WHERE SLD = 0
   
            IF OBJECT_ID(N'tempdb..#VW_SAS_TEMPR', N'U') IS NOT NULL  
            DROP TABLE #VW_SAS_TEMPR;
           
            PRINT '### => PEGA AS EMPRESAS DO PERIODO >>> #VW_SAS_TEMPR'
            print '@D_BASES'
            print @D_BASES
 
            CREATE TABLE #VW_SAS_TEMPR(CD_EMP INT,DS_EMP VARCHAR(50),STS_EMPR BIT,CD_PLN_EMP VARCHAR(100),CD_FECHTO CHAR(1),D_DATA_BASE DATE, IN_DEF INT)
            SET @STR_SQL="
            INSERT INTO #VW_SAS_TEMPR(CD_EMP,DS_EMP,STS_EMPR,CD_PLN_EMP,CD_FECHTO,D_DATA_BASE, IN_DEF)
            SELECT DISTINCT EMPR.CD_EMP,EMPR.DS_EMP,EMPR.STS_EMPR,EMPR.CD_PLN_EMP,EMPR.CD_FECHTO,EMPR.D_DATA_BASE, IN_DEF
 
            FROM " + @BANCO_LEITURA + "VW_SAS_TEMPR EMPR
 
            --INNER JOIN #TBALG FILTRO ON EMPR.D_DATA_BASE=FILTRO.D_BASE AND EMPR.CD_EMP=FILTRO.CD_EMP
 
            WHERE EMPR.D_DATA_BASE IN (" + @D_BASES + ")
 
            GROUP BY EMPR.CD_EMP,EMPR.DS_EMP,EMPR.STS_EMPR,EMPR.CD_PLN_EMP,EMPR.CD_FECHTO,EMPR.D_DATA_BASE, IN_DEF"
           
            print @STR_SQL
 
 
            EXEC (@STR_SQL)
 
            DECLARE @DTA VARCHAR(10) = dbo.GETS_NU_BASE_EM_D_BASE(@D_BASE)
            PRINT @DTA
            /*
                ARMS, 05-05-2025 - TRATATIVA INCLUÍDA PARA EMPRESAS COM DEFASAGEM. ID DE SOLICITAÇÃO SHAREPOINT 186
                Regra :
                 * Para as empresas com flag de defasagem temos que verificar o periodo informado no arquivo se corresponde ao mesmo informado na data de referencia considerando o numero de meses de defsasagem.
                 Ex. Se o arquivo estiver com periodo DEZ-2024 e a empresa estivar com defasagem de 1 mes (-1), significa que o periodo é valido e pode seguir a carga.
                 Para a carga continuar, temos que sobrepor a data base do arquivo e informa na coluna N_NRO_ANOMES_FIS com a data atual
 
                 Algoritimo :
                 D_BASE         : DATA DO CONTEUDO
                 @D_BASES       : DATA DO NOME DO ARQUIVO QUE DEVE CORRESPONDER A DATA COMPENETENCIA
                 NI_DEF         : Numero de meses de defagasem
                 D_BASE_ESPERADO = Volta {NI_DESF} meses a partir da data
                 D_BASE_ESPERADO = DATEADD(@D_BASES, ABS(NI_DESF) * (-1))
 
                 IF D_BASE_ESPERADO = D_BASE
 
                 UPDATE #TBALG SET D_BASE = CASE WHEN D_BASE_ESPERADO = D_BASE THEN @D_BASES ELSE D_BASE END
            */
 
           
            --return
 
 
            PRINT '### => TRATA EMPRESA COM DEFASAGEM'
            UPDATE A
                SET D_BASE =
            --select  
            CASE WHEN b.IN_DEF > 0 THEN CAST(DATEADD(M, b.IN_DEF,a.D_BASE) AS DATE) ELSE D_BASE END
            FROM  
                #TBALG A INNER JOIN #VW_SAS_TEMPR B ON A.CD_EMP = B.CD_EMP
 
 
            PRINT '### => TRATA MOEDA >> #CONF_MOEDA_REP'
 
            DROP TABLE IF EXISTS #CONF_MOEDA_REP
            CREATE TABLE #CONF_MOEDA_REP (D_DATA_BASE VARCHAR(10), CD_EMP VARCHAR(10), CD_MOE_REP VARCHAR(10))
            SET @STR_SQL ="
            INSERT INTO #CONF_MOEDA_REP
            SELECT DISTINCT D_DATA_BASE, CD_EMP, CD_MOE_REP FROM DB_CONSOLIDADO.DBO.VW_SAS_TEMPR
            WHERE D_DATA_BASE = '" + @DTA + "'"
            --PRINT @STR_SQL
            EXEC (@STR_SQL)
 
 
            PRINT '### => TRATA MOEDA >> #ERRO_MOEDA_REP'
 
            DROP TABLE IF EXISTS #ERRO_MOEDA_REP
            SELECT *
            INTO #ERRO_MOEDA_REP
            FROM(
                    SELECT DISTINCT C_ID_CTRL_PROCM
                                   ,A.CD_EMP
                                   ,A.MOE
                                   ,B.CD_MOE_REP
                                   ,CASE WHEN A.MOE <> B.CD_MOE_REP THEN 1 ELSE 0 END AS MOEDA_IGUAL
                    FROM #TBALG AS A
                    LEFT JOIN #CONF_MOEDA_REP AS B ON A.CD_EMP = B.CD_EMP) AS A
            WHERE MOEDA_IGUAL = 1
 
            DECLARE @ERRO_MOEDA_REPORT INT = (SELECT COUNT(*) FROM #ERRO_MOEDA_REP)
            IF @ERRO_MOEDA_REPORT > 0
                BEGIN
                    SET @STR_SQL=
                    "UPDATE" + @BANCO_LEITURA + "[T_CTRL_PROCM] SET R_DS_MSG = 'MOEDA DO ARQUIVO DIFERENTE DA MOEDA DE REPORT - BALG'
                                                                                ,C_COD_ERRO = 109
                                                                                ,D_DATA_PROCM = GETDATE()
                    WHERE C_ID_CTRL_PROCM IN (" + STUFF((SELECT DISTINCT ',' + CAST(B.C_ID_CTRL_PROCM AS VARCHAR)
                                                            FROM #ERRO_MOEDA_REP B
                                                            FOR XML PATH (''))
                                                            , 1, 1, '') + ")"
                    --PRINT @STR_SQL
                    EXEC (@STR_SQL)
 
                    GOTO FIM
                END
 
            PRINT '### => ATUALIZA FLAGS - CHAVE DA CONTA'
 
 
            UPDATE BALG SET
 
            -- select
             C_PL_EMP=ISNULL(TEMPR.CD_PLN_EMP,'-1')
            ,CH_FECHAMENTO=ISNULL(TEMPR.CD_FECHTO,'-1')
            ,STS_EMPR=ISNULL(TEMPR.STS_EMPR,'-1')
            ,FLG_EMP=CASE WHEN TEMPR.STS_EMPR IS NULL THEN 1 ELSE 0 END
            ,CD_CHV_CTA=TEMPR.CD_PLN_EMP + "#" + CD_CONTA
            ,FLG_NTCAO_CTFCA= CASE WHEN dbo.GETS_BUSCA_POS_CHAR(CD_CONTA,'E+')=0 THEN 0 ELSE 1 END
            ,FLG_DT_PROC_ARQ= CASE WHEN D_BASE IS NULL THEN 1 ELSE 0 END
           
            -- #ARMS, AQUI VERIFICA SE A DATA BASE DO ARQUIVO É A MESMA QUE A DATA BASE DE COMPETENCIA SOLICITADA. SE NAO, GERA O ERRO 127
            ,FLG_DT_IGUAL = CASE WHEN dbo.GETS_D_BASE_EM_NU_BASE(D_BASE) = dbo.FUNC_PEGAR_DATA_NOME_ARQUIVO(R_NOME_ARQ) THEN 0 ELSE 1 END
            --select *
            FROM #TBALG BALG LEFT JOIN #VW_SAS_TEMPR TEMPR ON BALG.CD_EMP=TEMPR.CD_EMP AND BALG.D_BASE=TEMPR.D_DATA_BASE
           
            DROP TABLE #VW_SAS_TEMPR
 
            DELETE FROM #TBALG WHERE C_PL_EMP='-1'
 
            PRINT '### => TRATA CADASTRO DO LAYOUT DA EMPRESA >>> #TB_EMP_VS_LAY'
            select * from #TBALG
 
            IF OBJECT_ID(N'tempdb..#TB_EMP_VS_LAY', N'U') IS NOT NULL  
            DROP TABLE #TB_EMP_VS_LAY;
            CREATE TABLE #TB_EMP_VS_LAY(R_NOME_ARQ VARCHAR (8000),D_DATA_BASE DATE, R_CD_EMPR varchar(max))
 
 
            SET @STR_SQL ="
            INSERT INTO #TB_EMP_VS_LAY(R_NOME_ARQ
                                       ,D_DATA_BASE
                                       ,R_CD_EMPR)
            SELECT
            ARQ.R_NOME_ARQ
            ,ARQ.D_DATA_BASE
            ,R_CD_EMPR
            FROM " + @BANCO_LEITURA + "VW_SAS_LYOUT_ARQ AS ARQ
            INNER JOIN #T_ARQ_CTRL_PROCM FILTRO ON ARQ.R_NOME_ARQ=FILTRO.R_NOME_ARQ_SAS
            AND ARQ.D_DATA_BASE=FILTRO.D_BASE
            GROUP BY ARQ.R_NOME_ARQ,ARQ.D_DATA_BASE, R_CD_EMPR"
            --print @STR_SQL
            EXEC (@STR_SQL)
 
            UPDATE #TBALG  SET
                   FLG_EMP_VS_LAY = CASE WHEN C.R_NOME_ARQ IS NULL THEN 1 ELSE 0 END
            FROM #TBALG A
                INNER JOIN #T_ARQ_CTRL_PROCM B ON A.C_ID_ARQ_CTRL_PROCM = B.C_ID_ARQ_CTRL_PROCM
                LEFT JOIN #TB_EMP_VS_LAY C ON B.R_NOME_ARQ_SAS = C.R_NOME_ARQ AND CAST(A.CD_EMP AS INT) = CAST(C.R_CD_EMPR AS INT)
 
            DROP TABLE #TB_EMP_VS_LAY
 
            PRINT '### => TRATA PLANO DE CONTAS (COSIF) >>> #VW_SAS_PLANO_CONTAS'
 
            IF OBJECT_ID(N'tempdb..#VW_SAS_PLANO_CONTAS', N'U') IS NOT NULL  
            DROP TABLE #VW_SAS_PLANO_CONTAS;
 
            CREATE TABLE #VW_SAS_PLANO_CONTAS(CD_RAZAO VARCHAR (30),CD_CONTA VARCHAR (30),CD_COSIF VARCHAR (30),CD_CHV_CTA VARCHAR (50),DS_ORIGEM VARCHAR (8),D_DATA_BASE DATE)
 
            SET @STR_SQL="
            INSERT INTO #VW_SAS_PLANO_CONTAS(CD_CONTA,CD_RAZAO,CD_COSIF,CD_CHV_CTA,DS_ORIGEM,D_DATA_BASE)
            SELECT DISTINCT a.C_NRO_CTA AS CD_CONTA, a.C_RZ_CTA AS CD_RAZAO, d.C_NRO_CTA_CONSLD AS CD_COSIF,a.R_CHAVE_CTA AS CD_CHV_CTA,CASE WHEN A.N_FL_AJUST = 0 THEN 'EMPRESAS' ELSE 'AJUSTES' END AS DS_ORIGEM, c.D_DATA_BASE
            FROM " + @BANCO_LEITURA + "T_CTA_INTRN AS a INNER JOIN " + @BANCO_LEITURA + "T_DOMNO AS b ON b.C_ID_DOMNO = a.C_ID_PLANO_CTA INNER JOIN " + @BANCO_LEITURA + "T_DEPARA_CTA AS c ON c.R_CHAVE_CTA = a.R_CHAVE_CTA INNER JOIN " + @BANCO_LEITURA + "T_CTA_CONSLD AS d ON d.D_CHAVE_CTA_CONSLD = c.D_CHAVE_CTA_CONSLD AND d.D_DATA_BASE = c.D_DATA_BASE
            INNER JOIN #TBALG ON #TBALG.CD_CHV_CTA=a.R_CHAVE_CTA AND #TBALG.D_BASE=d.D_DATA_BASE
            WHERE d.C_ID_PLANO_CTA_CONSLD = 319 and c.U_STTUS=1
            GROUP BY a.C_RZ_CTA, a.C_NRO_CTA, d.C_NRO_CTA_CONSLD, a.R_CHAVE_CTA, c.D_DATA_BASE,A.N_FL_AJUST"
 
            print @STR_SQL
 
            EXEC (@STR_SQL)
 
            UPDATE #TBALG SET
                             CD_COSIF=ISNULL(PL.CD_COSIF,'-1')
                            ,CD_RAZAO=ISNULL(PL.CD_RAZAO,'-1')
                            ,DS_ORIGEM=ISNULL(PL.DS_ORIGEM,'-1')
                            ,FLG_PCTA=CASE WHEN PL.DS_ORIGEM IS NULL THEN 1 ELSE 0 END
                            ,FLG_CTA_AJU=CASE WHEN TRIM(ISNULL(PL.DS_ORIGEM,'-1'))= 'AJUSTES' THEN 1 ELSE 0 END
            FROM #TBALG LEFT JOIN #VW_SAS_PLANO_CONTAS PL ON #TBALG.CD_CHV_CTA=PL.CD_CHV_CTA AND #TBALG.D_BASE=PL.D_DATA_BASE
 
            DROP TABLE #VW_SAS_PLANO_CONTAS
 
            PRINT '### => TRATA SALDO ZERADO'
 
            UPDATE #TBALG SET FLG_SLD_ZRO=CASE WHEN ZERADO.R_NOME_ARQ IS NULL THEN 1 ELSE 0 END
            FROM (
            SELECT R_NOME_ARQ
            FROM #TBALG
            GROUP BY R_NOME_ARQ
            HAVING SUM(SLD) BETWEEN -0.05 AND 0.05) AS ZERADO RIGHT JOIN #TBALG ON ZERADO.R_NOME_ARQ=#TBALG.R_NOME_ARQ
 
            PRINT '### => VERIFICA SE HA ERROS EM ALGUM DOS FLAGS'
 
            DECLARE @ERROS INT = (SELECT COUNT(*) FROM #TBALG
                                  WHERE
                                     FLG_CTA_AJU = 1
                                  OR FLG_EMP = 1
                                  OR FLG_EMP_VS_LAY = 1
                                  OR FLG_NTCAO_CTFCA = 1
                                  OR FLG_DT_PROC_ARQ = 1
                                  OR FLG_SLD_ZRO = 1
                                  OR FLG_PCTA = 1
                                  OR FLG_DT_IGUAL = 1)
 
            PRINT "TOTAL DE ERROS = " + CAST(@ERROS AS VARCHAR(MAX))
 
            SET @ARRAY_ERRO = STUFF((SELECT DISTINCT ',' + CAST(B.C_ID_CTRL_PROCM AS VARCHAR)
                                        FROM #TBALG B
                                        WHERE
                                             FLG_CTA_AJU = 1
                                          OR FLG_EMP = 1
                                          OR FLG_EMP_VS_LAY = 1
                                          OR FLG_NTCAO_CTFCA = 1
                                          OR FLG_DT_PROC_ARQ = 1
                                          OR FLG_SLD_ZRO = 1
                                          OR FLG_PCTA = 1
                                          OR FLG_DT_IGUAL = 1
                                        FOR XML PATH (''))
                                        , 1, 1, '')
            PRINT "ERROS = " + @ARRAY_ERRO
 
            SET @ARRAY_OK = STUFF((SELECT DISTINCT ',' + CAST(B.C_ID_CTRL_PROCM AS VARCHAR)
                                    FROM #TBALG B
                                    WHERE
                                            FLG_CTA_AJU = 0
                                        AND FLG_EMP = 0
                                        AND FLG_EMP_VS_LAY = 0
                                        AND FLG_NTCAO_CTFCA = 0
                                        AND FLG_DT_PROC_ARQ = 0
                                        AND FLG_SLD_ZRO = 0
                                        AND FLG_PCTA = 0
                                        AND FLG_DT_IGUAL = 0
                                    FOR XML PATH (''))
                                    , 1, 1, '')
            PRINT "OK = " + @ARRAY_OK
 
 
            SET @ARRAY_ARQ_ERRO = ISNULL(STUFF((SELECT DISTINCT ",'" + CAST(B.R_NOME_ARQ AS VARCHAR) + "'"
                                        FROM #TBALG B
                                        WHERE
                                             FLG_CTA_AJU = 1
                                          OR FLG_EMP = 1
                                          OR FLG_EMP_VS_LAY = 1
                                          OR FLG_NTCAO_CTFCA = 1
                                          OR FLG_DT_PROC_ARQ = 1
                                          OR FLG_SLD_ZRO = 1
                                          OR FLG_PCTA = 1
                                          OR FLG_DT_IGUAL = 1
                                        FOR XML PATH (''))
                                        , 1, 1, ''),"'ERRO'")
 
            PRINT "ARQS COM ERROS = " + @ARRAY_ARQ_ERRO
 
            PRINT '### => GRAVA TABELA DE ERROS : T_PCONP_BASE_CONSL_ANLTCA_LOGERR'
 
            SET @STR_SQL="
            INSERT INTO "+ @BANCO_GRAVA + "T_PCONP_BASE_CONSL_ANLTCA_LOGERR(N_COD_ORIGE,N_COD_ORIGE_N02,C_COD_EMPR,C_COD_PLANO_EMPR,C_COD_RZ,C_COD_CTA,C_COD_COSIF,C_COD_CHV_CTA,N_NRO_ANOMES,N_NRO_ANOMES_FIS,C_COD_PRAZO,C_COD_MOEDA,C_COD_GRP_CONSL,C_COD_EMP_INVSTDA,C_COD_EMP_INVSTDORA,C_COD_AJUST,C_COD_CHV_PARTES,V_SDO,R_NOME_ARQ,D_DATA_PROCM,N_COD_VERSAO,C_COD_FECHTO,C_COD_PCERO,C_COD_JUNC,C_ID_GRP_ELIMI,R_FLAG_GRP_ELIMI,V_VLR_SDO_TOT,V_VLR_DIFCA,R_FLAG_VLR_DIFCA,C_GRP_CONSL_EMPR,P_PERC_PRTCP_EMPR,C_GRP_CONSL_PCERO,P_PERC_PRTCP_PCERO,R_TPO_CONSL,C_COD_CHV_PARTES_SOMA,R_FLAG_CRTCA_PAR,V_SDO_TELA,C_COD_ELIM,R_FLG_STTUS_CARGA_ARQ,C_ID_CTRL_PROCM,C_ID_ARQ_CTRL_PROCM)
            SELECT 102 AS N_COD_ORIGE, 2200 AS N_COD_ORIGE_N02, RIGHT('00000' + CD_EMP,5) AS C_COD_EMPR, C_PL_EMP AS C_COD_PLANO_EMPR, CD_RAZAO AS C_COD_RZ, CD_CONTA AS C_COD_CTA, CD_COSIF AS C_COD_COSIF, CD_CHV_CTA AS C_COD_CHV_CTA,dbo.GETS_D_BASE_EM_NU_BASE(D_BASE) AS N_NRO_ANOMES, dbo.GETS_D_BASE_EM_NU_BASE(D_BASE) AS N_NRO_ANOMES_FIS, PRZ AS C_COD_PRAZO,MOE AS C_COD_MOEDA, -1 AS C_COD_GRP_CONSL, -1 AS C_COD_EMP_INVSTDA, -1 AS C_COD_EMP_INVSTDORA, -1 AS C_COD_AJUST,-1 AS C_COD_CHV_PARTES, SLD AS V_SDO, R_NOME_ARQ AS R_NOME_ARQ, GETDATE() AS D_DATA_PROCM, 1 AS N_COD_VERSAO, CH_FECHAMENTO AS C_COD_FECHTO, -1 AS C_COD_PCERO,-1 AS C_COD_JUNC, -1 AS C_ID_GRP_ELIMI, -1 AS R_FLAG_GRP_ELIMI, 0 AS V_VLR_SDO_TOT, 0 AS V_VLR_DIFCA, -1 AS R_FLAG_VLR_DIFCA, -1 AS C_GRP_CONSL_EMPR, 0 AS P_PERC_PRTCP_EMPR, -1 AS C_GRP_CONSL_PCERO, 0 AS P_PERC_PRTCP_PCERO, -1 AS R_TPO_CONSL, -1 AS C_COD_CHV_PARTES_SOMA, -1 AS R_FLAG_CRTCA_PAR ,0 AS V_SDO_TELA, -1 AS C_COD_ELIM, 108 AS R_FLG_STTUS_CARGA_ARQ,C_ID_CTRL_PROCM,C_ID_ARQ_CTRL_PROCM
            FROM #TBALG
            WHERE FLG_CTA_AJU=1
                UNION ALL
            SELECT 102 AS N_COD_ORIGE, 2200 AS N_COD_ORIGE_N02, RIGHT('00000' + CD_EMP,5) AS C_COD_EMPR, C_PL_EMP AS C_COD_PLANO_EMPR, CD_RAZAO AS C_COD_RZ, CD_CONTA AS C_COD_CTA, CD_COSIF AS C_COD_COSIF, CD_CHV_CTA AS C_COD_CHV_CTA,dbo.GETS_D_BASE_EM_NU_BASE(D_BASE) AS N_NRO_ANOMES, dbo.GETS_D_BASE_EM_NU_BASE(D_BASE) AS N_NRO_ANOMES_FIS, PRZ AS C_COD_PRAZO,MOE AS C_COD_MOEDA, -1 AS C_COD_GRP_CONSL, -1 AS C_COD_EMP_INVSTDA, -1 AS C_COD_EMP_INVSTDORA, -1 AS C_COD_AJUST,-1 AS C_COD_CHV_PARTES, SLD AS V_SDO, R_NOME_ARQ AS R_NOME_ARQ, GETDATE() AS D_DATA_PROCM, 1 AS N_COD_VERSAO, CH_FECHAMENTO AS C_COD_FECHTO, -1 AS C_COD_PCERO,-1 AS C_COD_JUNC, -1 AS C_ID_GRP_ELIMI, -1 AS R_FLAG_GRP_ELIMI, 0 AS V_VLR_SDO_TOT, 0 AS V_VLR_DIFCA, -1 AS R_FLAG_VLR_DIFCA, -1 AS C_GRP_CONSL_EMPR, 0 AS P_PERC_PRTCP_EMPR, -1 AS C_GRP_CONSL_PCERO, 0 AS P_PERC_PRTCP_PCERO, -1 AS R_TPO_CONSL, -1 AS C_COD_CHV_PARTES_SOMA, -1 AS R_FLAG_CRTCA_PAR ,0 AS V_SDO_TELA, -1 AS C_COD_ELIM, 103 AS R_FLG_STTUS_CARGA_ARQ,C_ID_CTRL_PROCM,C_ID_ARQ_CTRL_PROCM
            FROM #TBALG
            WHERE FLG_PCTA=1
                UNION ALL
            SELECT 102 AS N_COD_ORIGE, 2200 AS N_COD_ORIGE_N02, RIGHT('00000' + CD_EMP,5) AS C_COD_EMPR, C_PL_EMP AS C_COD_PLANO_EMPR, CD_RAZAO AS C_COD_RZ, CD_CONTA AS C_COD_CTA, CD_COSIF AS C_COD_COSIF, CD_CHV_CTA AS C_COD_CHV_CTA,dbo.GETS_D_BASE_EM_NU_BASE(D_BASE) AS N_NRO_ANOMES, dbo.GETS_D_BASE_EM_NU_BASE(D_BASE) AS N_NRO_ANOMES_FIS, PRZ AS C_COD_PRAZO,MOE AS C_COD_MOEDA, -1 AS C_COD_GRP_CONSL, -1 AS C_COD_EMP_INVSTDA, -1 AS C_COD_EMP_INVSTDORA, -1 AS C_COD_AJUST,-1 AS C_COD_CHV_PARTES, SLD AS V_SDO, R_NOME_ARQ AS R_NOME_ARQ, GETDATE() AS D_DATA_PROCM, 1 AS N_COD_VERSAO, CH_FECHAMENTO AS C_COD_FECHTO, -1 AS C_COD_PCERO,-1 AS C_COD_JUNC, -1 AS C_ID_GRP_ELIMI, -1 AS R_FLAG_GRP_ELIMI, 0 AS V_VLR_SDO_TOT, 0 AS V_VLR_DIFCA, -1 AS R_FLAG_VLR_DIFCA, -1 AS C_GRP_CONSL_EMPR, 0 AS P_PERC_PRTCP_EMPR, -1 AS C_GRP_CONSL_PCERO, 0 AS P_PERC_PRTCP_PCERO, -1 AS R_TPO_CONSL, -1 AS C_COD_CHV_PARTES_SOMA, -1 AS R_FLAG_CRTCA_PAR ,0 AS V_SDO_TELA, -1 AS C_COD_ELIM, 102 AS R_FLG_STTUS_CARGA_ARQ,C_ID_CTRL_PROCM,C_ID_ARQ_CTRL_PROCM
            FROM #TBALG
            WHERE FLG_EMP=1
                UNION ALL
            SELECT 102 AS N_COD_ORIGE, 2200 AS N_COD_ORIGE_N02, RIGHT('00000' + CD_EMP,5) AS C_COD_EMPR, C_PL_EMP AS C_COD_PLANO_EMPR, CD_RAZAO AS C_COD_RZ, CD_CONTA AS C_COD_CTA, CD_COSIF AS C_COD_COSIF, CD_CHV_CTA AS C_COD_CHV_CTA,dbo.GETS_D_BASE_EM_NU_BASE(D_BASE) AS N_NRO_ANOMES, dbo.GETS_D_BASE_EM_NU_BASE(D_BASE) AS N_NRO_ANOMES_FIS, PRZ AS C_COD_PRAZO,MOE AS C_COD_MOEDA, -1 AS C_COD_GRP_CONSL, -1 AS C_COD_EMP_INVSTDA, -1 AS C_COD_EMP_INVSTDORA, -1 AS C_COD_AJUST,-1 AS C_COD_CHV_PARTES, SLD AS V_SDO, R_NOME_ARQ AS R_NOME_ARQ, GETDATE() AS D_DATA_PROCM, 1 AS N_COD_VERSAO, CH_FECHAMENTO AS C_COD_FECHTO, -1 AS C_COD_PCERO,-1 AS C_COD_JUNC, -1 AS C_ID_GRP_ELIMI, -1 AS R_FLAG_GRP_ELIMI, 0 AS V_VLR_SDO_TOT, 0 AS V_VLR_DIFCA, -1 AS R_FLAG_VLR_DIFCA, -1 AS C_GRP_CONSL_EMPR, 0 AS P_PERC_PRTCP_EMPR, -1 AS C_GRP_CONSL_PCERO, 0 AS P_PERC_PRTCP_PCERO, -1 AS R_TPO_CONSL, -1 AS C_COD_CHV_PARTES_SOMA, -1 AS R_FLAG_CRTCA_PAR ,0 AS V_SDO_TELA, -1 AS C_COD_ELIM, 120 AS R_FLG_STTUS_CARGA_ARQ,C_ID_CTRL_PROCM,C_ID_ARQ_CTRL_PROCM
            FROM #TBALG
            WHERE FLG_EMP_VS_LAY=1
                UNION ALL
            SELECT 102 AS N_COD_ORIGE, 2200 AS N_COD_ORIGE_N02, RIGHT('00000' + CD_EMP,5) AS C_COD_EMPR, C_PL_EMP AS C_COD_PLANO_EMPR, CD_RAZAO AS C_COD_RZ, CD_CONTA AS C_COD_CTA, CD_COSIF AS C_COD_COSIF, CD_CHV_CTA AS C_COD_CHV_CTA,dbo.GETS_D_BASE_EM_NU_BASE(D_BASE) AS N_NRO_ANOMES, dbo.GETS_D_BASE_EM_NU_BASE(D_BASE) AS N_NRO_ANOMES_FIS, PRZ AS C_COD_PRAZO,MOE AS C_COD_MOEDA, -1 AS C_COD_GRP_CONSL, -1 AS C_COD_EMP_INVSTDA, -1 AS C_COD_EMP_INVSTDORA, -1 AS C_COD_AJUST,-1 AS C_COD_CHV_PARTES, SLD AS V_SDO, R_NOME_ARQ AS R_NOME_ARQ, GETDATE() AS D_DATA_PROCM, 1 AS N_COD_VERSAO, CH_FECHAMENTO AS C_COD_FECHTO, -1 AS C_COD_PCERO,-1 AS C_COD_JUNC, -1 AS C_ID_GRP_ELIMI, -1 AS R_FLAG_GRP_ELIMI, 0 AS V_VLR_SDO_TOT, 0 AS V_VLR_DIFCA, -1 AS R_FLAG_VLR_DIFCA, -1 AS C_GRP_CONSL_EMPR, 0 AS P_PERC_PRTCP_EMPR, -1 AS C_GRP_CONSL_PCERO, 0 AS P_PERC_PRTCP_PCERO, -1 AS R_TPO_CONSL, -1 AS C_COD_CHV_PARTES_SOMA, -1 AS R_FLAG_CRTCA_PAR ,0 AS V_SDO_TELA, -1 AS C_COD_ELIM, 112 AS R_FLG_STTUS_CARGA_ARQ,C_ID_CTRL_PROCM,C_ID_ARQ_CTRL_PROCM
            FROM #TBALG
            WHERE FLG_NTCAO_CTFCA=1
                UNION ALL
            SELECT 102 AS N_COD_ORIGE, 2200 AS N_COD_ORIGE_N02, RIGHT('00000' + CD_EMP,5) AS C_COD_EMPR, C_PL_EMP AS C_COD_PLANO_EMPR, CD_RAZAO AS C_COD_RZ, CD_CONTA AS C_COD_CTA, CD_COSIF AS C_COD_COSIF, CD_CHV_CTA AS C_COD_CHV_CTA,dbo.GETS_D_BASE_EM_NU_BASE(D_BASE) AS N_NRO_ANOMES, dbo.GETS_D_BASE_EM_NU_BASE(D_BASE) AS N_NRO_ANOMES_FIS, PRZ AS C_COD_PRAZO,MOE AS C_COD_MOEDA, -1 AS C_COD_GRP_CONSL, -1 AS C_COD_EMP_INVSTDA, -1 AS C_COD_EMP_INVSTDORA, -1 AS C_COD_AJUST,-1 AS C_COD_CHV_PARTES, SLD AS V_SDO, R_NOME_ARQ AS R_NOME_ARQ, GETDATE() AS D_DATA_PROCM, 1 AS N_COD_VERSAO, CH_FECHAMENTO AS C_COD_FECHTO, -1 AS C_COD_PCERO,-1 AS C_COD_JUNC, -1 AS C_ID_GRP_ELIMI, -1 AS R_FLAG_GRP_ELIMI, 0 AS V_VLR_SDO_TOT, 0 AS V_VLR_DIFCA, -1 AS R_FLAG_VLR_DIFCA, -1 AS C_GRP_CONSL_EMPR, 0 AS P_PERC_PRTCP_EMPR, -1 AS C_GRP_CONSL_PCERO, 0 AS P_PERC_PRTCP_PCERO, -1 AS R_TPO_CONSL, -1 AS C_COD_CHV_PARTES_SOMA, -1 AS R_FLAG_CRTCA_PAR ,0 AS V_SDO_TELA, -1 AS C_COD_ELIM, 106 AS R_FLG_STTUS_CARGA_ARQ,C_ID_CTRL_PROCM,C_ID_ARQ_CTRL_PROCM
            FROM #TBALG
            WHERE FLG_DT_PROC_ARQ=1
                UNION ALL
            SELECT 102 AS N_COD_ORIGE, 2200 AS N_COD_ORIGE_N02, RIGHT('00000' + CD_EMP,5) AS C_COD_EMPR, C_PL_EMP AS C_COD_PLANO_EMPR, CD_RAZAO AS C_COD_RZ, CD_CONTA AS C_COD_CTA, CD_COSIF AS C_COD_COSIF, CD_CHV_CTA AS C_COD_CHV_CTA,dbo.GETS_D_BASE_EM_NU_BASE(D_BASE) AS N_NRO_ANOMES, dbo.GETS_D_BASE_EM_NU_BASE(D_BASE) AS N_NRO_ANOMES_FIS, PRZ AS C_COD_PRAZO,MOE AS C_COD_MOEDA, -1 AS C_COD_GRP_CONSL, -1 AS C_COD_EMP_INVSTDA, -1 AS C_COD_EMP_INVSTDORA, -1 AS C_COD_AJUST,-1 AS C_COD_CHV_PARTES, SLD AS V_SDO, R_NOME_ARQ AS R_NOME_ARQ, GETDATE() AS D_DATA_PROCM, 1 AS N_COD_VERSAO, CH_FECHAMENTO AS C_COD_FECHTO, -1 AS C_COD_PCERO,-1 AS C_COD_JUNC, -1 AS C_ID_GRP_ELIMI, -1 AS R_FLAG_GRP_ELIMI, 0 AS V_VLR_SDO_TOT, 0 AS V_VLR_DIFCA, -1 AS R_FLAG_VLR_DIFCA, -1 AS C_GRP_CONSL_EMPR, 0 AS P_PERC_PRTCP_EMPR, -1 AS C_GRP_CONSL_PCERO, 0 AS P_PERC_PRTCP_PCERO, -1 AS R_TPO_CONSL, -1 AS C_COD_CHV_PARTES_SOMA, -1 AS R_FLAG_CRTCA_PAR ,0 AS V_SDO_TELA, -1 AS C_COD_ELIM, 105 AS R_FLG_STTUS_CARGA_ARQ,C_ID_CTRL_PROCM,C_ID_ARQ_CTRL_PROCM
            FROM #TBALG
            WHERE FLG_SLD_ZRO=1
                UNION ALL
            SELECT 102 AS N_COD_ORIGE, 2200 AS N_COD_ORIGE_N02, RIGHT('00000' + CD_EMP,5) AS C_COD_EMPR, C_PL_EMP AS C_COD_PLANO_EMPR, CD_RAZAO AS C_COD_RZ, CD_CONTA AS C_COD_CTA, CD_COSIF AS C_COD_COSIF, CD_CHV_CTA AS C_COD_CHV_CTA,dbo.GETS_D_BASE_EM_NU_BASE(D_BASE) AS N_NRO_ANOMES, dbo.GETS_D_BASE_EM_NU_BASE(D_BASE) AS N_NRO_ANOMES_FIS, PRZ AS C_COD_PRAZO,MOE AS C_COD_MOEDA, -1 AS C_COD_GRP_CONSL, -1 AS C_COD_EMP_INVSTDA, -1 AS C_COD_EMP_INVSTDORA, -1 AS C_COD_AJUST,-1 AS C_COD_CHV_PARTES, SLD AS V_SDO, R_NOME_ARQ AS R_NOME_ARQ, GETDATE() AS D_DATA_PROCM, 1 AS N_COD_VERSAO, CH_FECHAMENTO AS C_COD_FECHTO, -1 AS C_COD_PCERO,-1 AS C_COD_JUNC, -1 AS C_ID_GRP_ELIMI, -1 AS R_FLAG_GRP_ELIMI, 0 AS V_VLR_SDO_TOT, 0 AS V_VLR_DIFCA, -1 AS R_FLAG_VLR_DIFCA, -1 AS C_GRP_CONSL_EMPR, 0 AS P_PERC_PRTCP_EMPR, -1 AS C_GRP_CONSL_PCERO, 0 AS P_PERC_PRTCP_PCERO, -1 AS R_TPO_CONSL, -1 AS C_COD_CHV_PARTES_SOMA, -1 AS R_FLAG_CRTCA_PAR ,0 AS V_SDO_TELA, -1 AS C_COD_ELIM, 127 AS R_FLG_STTUS_CARGA_ARQ,C_ID_CTRL_PROCM,C_ID_ARQ_CTRL_PROCM
            FROM #TBALG
            WHERE FLG_DT_IGUAL=1"
 
            PRINT @STR_SQL
 
 
            EXEC (@STR_SQL)
           
                   
 
            --IF @ERROS = 0
                --BEGIN
                    SET @STR_SQL="
                    INSERT INTO "+ @BANCO_GRAVA + "T_PCONP_BASE_CONSL_ANLTCA(N_COD_ORIGE,N_COD_ORIGE_N02,C_COD_EMPR,C_COD_PLANO_EMPR,C_COD_RZ,C_COD_CTA,C_COD_COSIF,C_COD_CHV_CTA,N_NRO_ANOMES,N_NRO_ANOMES_FIS,C_COD_PRAZO,C_COD_MOEDA,C_COD_GRP_CONSL,C_COD_EMP_INVSTDA,C_COD_EMP_INVSTDORA,C_COD_AJUST,C_COD_CHV_PARTES,V_SDO,R_NOME_ARQ,D_DATA_PROCM,N_COD_VERSAO,C_COD_FECHTO,C_COD_PCERO,C_COD_JUNC,C_ID_GRP_ELIMI,R_FLAG_GRP_ELIMI,V_VLR_SDO_TOT,V_VLR_DIFCA,R_FLAG_VLR_DIFCA,C_GRP_CONSL_EMPR,P_PERC_PRTCP_EMPR,C_GRP_CONSL_PCERO,P_PERC_PRTCP_PCERO,R_TPO_CONSL,C_COD_CHV_PARTES_SOMA,R_FLAG_CRTCA_PAR,V_SDO_TELA,C_COD_ELIM,C_ID_CTRL_PROCM)
                    SELECT 102 AS N_COD_ORIGE, 2200 AS N_COD_ORIGE_N02, RIGHT('00000' + CD_EMP,5) AS C_COD_EMPR, C_PL_EMP AS C_COD_PLANO_EMPR, CD_RAZAO AS C_COD_RZ, CD_CONTA AS C_COD_CTA, CD_COSIF AS C_COD_COSIF, CD_CHV_CTA AS C_COD_CHV_CTA,dbo.GETS_D_BASE_EM_NU_BASE(D_BASE) AS N_NRO_ANOMES, dbo.GETS_D_BASE_EM_NU_BASE(D_BASE) AS N_NRO_ANOMES_FIS, PRZ AS C_COD_PRAZO,MOE AS C_COD_MOEDA, -1 AS C_COD_GRP_CONSL, -1 AS C_COD_EMP_INVSTDA, -1 AS C_COD_EMP_INVSTDORA, -1 AS C_COD_AJUST,-1 AS C_COD_CHV_PARTES, SLD AS V_SDO, R_NOME_ARQ AS R_NOME_ARQ, GETDATE() AS D_DATA_PROCM, 1 AS N_COD_VERSAO, CH_FECHAMENTO AS C_COD_FECHTO, -1 AS C_COD_PCERO,-1 AS C_COD_JUNC, -1 AS C_ID_GRP_ELIMI, -1 AS R_FLAG_GRP_ELIMI, 0 AS V_VLR_SDO_TOT, 0 AS V_VLR_DIFCA, -1 AS R_FLAG_VLR_DIFCA, -1 AS C_GRP_CONSL_EMPR, 0 AS P_PERC_PRTCP_EMPR, -1 AS C_GRP_CONSL_PCERO, 0 AS P_PERC_PRTCP_PCERO, -1 AS R_TPO_CONSL, -1 AS C_COD_CHV_PARTES_SOMA, -1 AS R_FLAG_CRTCA_PAR ,0 AS V_SDO_TELA, -1 AS C_COD_ELIM,C_ID_CTRL_PROCM
                    FROM #TBALG
                    WHERE FLG_CTA_AJU=0 AND FLG_EMP=0 AND FLG_EMP_VS_LAY=0 AND FLG_NTCAO_CTFCA=0 AND FLG_DT_PROC_ARQ=0 AND FLG_SLD_ZRO=0 AND FLG_PCTA = 0 AND FLG_DT_IGUAL = 0
                    AND R_NOME_ARQ NOT IN (" + @ARRAY_ARQ_ERRO + ")"
                    --PRINT(@STR_SQL)
 
                    PRINT '### => GRAVA TABELA DE DADOS FINAL : T_PCONP_BASE_CONSL_ANLTCA'
 
                    EXEC (@STR_SQL)
                --END
 
            --- ATUALIZA MENSAGERIA DE ERRO
           
   
       
           
        END
 
        PRINT '### => ATUALIZA STATUS DA CARGA : @ARRAY_OK = ' + CAST(@ARRAY_OK AS VARCHAR)
 
        SET @STR_SQL=
            "UPDATE" + @BANCO_LEITURA + "[T_CTRL_PROCM] SET R_DS_MSG = 'PROCESSAMENTO SQL OK - BALG'
                                                                        ,C_COD_ERRO = 100
                                                                        ,D_DATA_PROCM = GETDATE()
            WHERE C_ID_CTRL_PROCM IN (" + @ARRAY_OK + ")"
            --PRINT @STR_SQL
            EXEC (@STR_SQL)
 
        PRINT '### => ATUALIZA STATUS DA CARGA : @ARRAY_ERRO = ' + CAST(@ARRAY_ERRO AS VARCHAR)
 
        SET @STR_SQL=
            "UPDATE" + @BANCO_LEITURA + "[T_CTRL_PROCM] SET R_DS_MSG = 'CRITICAS IDENTIFICADAS NO PROCESSAMENTO, VERIFICAR LOG DE PROCESSAMENTO'
                                                                        ,C_COD_ERRO = 122
                                                                        ,D_DATA_PROCM = GETDATE()
            WHERE C_ID_CTRL_PROCM IN (" + @ARRAY_ERRO + ")"
            --PRINT @STR_SQL
            EXEC (@STR_SQL)
           
 
       
--FIM ATUALIZA MENSAGERIA DE ERRO      
 
        UPDATE TPROCM SET R_DESCR='PROCESSADO COM SUCESSO' WHERE R_PROC='PROC_INSC_BALG'
        PRINT 'DEU BOA'
FIM:
        COMMIT TRANSACTION  
    END TRY
    BEGIN CATCH
        ROLLBACK TRANSACTION
       
            DECLARE @ERROSQL VARCHAR(MAX) = REPLACE(ERROR_MESSAGE(),"'","")
            PRINT "ERRO = " + REPLACE(@ERROSQL,"'","")
            PRINT "C_ID_ARQ_CTRL_PROCM = " + @C_ID_ARQ_CTRL_PROCM
            SET @STR_SQL=
            "UPDATE" + @BANCO_LEITURA + "[T_CTRL_PROCM] SET R_DS_MSG = 'ERRO DE SQL = " + @ERROSQL + " - BALG'
                                                                        ,C_COD_ERRO = 111
                                                                        ,D_DATA_PROCM = GETDATE()
            WHERE C_ID_CTRL_PROCM IN (" + @C_ID_ARQ_CTRL_PROCM + ")"
            PRINT @STR_SQL
            EXEC (@STR_SQL)
 
            UPDATE TPROCM SET R_DESCR=ERROR_MESSAGE() WHERE R_PROC='PROC_INSC_BALG'
 
    END CATCH
END
 
go
 