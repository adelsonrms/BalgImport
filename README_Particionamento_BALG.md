
# 🏗️ Estratégia de Particionamento - Processo BALG

## 📅 SQL Server 2019
**Empresa:** BRADESCO CONTADORIA  
**Objetivo:** Otimizar performance, escalabilidade e manutenção da base analítica `T_PCONP_BASE_CONSL_ANLTCA`.

---

## 🎯 Objetivo do Particionamento
- ✅ Melhorar performance de consultas e cargas.
- ✅ Reduzir lock e contenção.
- ✅ Facilitar arquivamento, manutenção e gerenciamento.
- ✅ Permitir operação sobre grandes volumes de dados com maior eficiência.

---

## 🏗️ Estrutura de Particionamento

### 🔹 **Coluna de Particionamento:** `D_BASE`  
(Representa a data base do dado.)

### 🔹 **Esquema:** Por ano. (Exemplo: 2022, 2023, 2024...)

---

## 🛠️ Scripts SQL

### 1️⃣ Criação da Função de Particionamento

```sql
CREATE PARTITION FUNCTION pfDataBase (DATE)
AS RANGE LEFT FOR VALUES 
('2022-12-31', '2023-12-31', '2024-12-31', '2025-12-31', '2026-12-31');
```

### 2️⃣ Criação do Esquema de Particionamento

```sql
CREATE PARTITION SCHEME psDataBase
AS PARTITION pfDataBase
ALL TO ([PRIMARY]);
```

### 3️⃣ Criação da Tabela com Particionamento

```sql
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
```

---

## 🔍 Verificar Partições

```sql
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
```

---

## 🔄 Manutenção de Partições

### ➕ Adicionar Novo Ano (Ex.: 2027)
```sql
ALTER PARTITION FUNCTION pfDataBase()
SPLIT RANGE ('2027-12-31');
```

### ➖ Remover Dados Antigos (Ex.: 2022)
```sql
ALTER PARTITION FUNCTION pfDataBase()
MERGE RANGE ('2022-12-31');
```

---

## ⚙️ Vantagens
- 🚀 Acesso otimizado por `D_BASE`.
- 🔥 Cargas e consultas muito mais rápidas.
- 🏗️ Manutenção simplificada (arquivar, excluir ou mover dados facilmente).

---

## 💼 Observações Importantes
- ✔️ Ideal para bases analíticas de alta volumetria.
- ✔️ Extensível para particionamento por `C_ID_UPLOAD` caso necessário.
- ✔️ Estratégia escalável e aderente às melhores práticas de mercado.

---

## 👨‍💻 Próximos Passos (Sugeridos)
- 🔧 Criar rotinas automáticas para split/merge de partições.
- 📊 Criar dashboards de monitoramento de tamanho e distribuição das partições.
- 🔗 Integrar com jobs de manutenção e arquivamento.

---

© BRADESCO CONTADORIA - 2025
