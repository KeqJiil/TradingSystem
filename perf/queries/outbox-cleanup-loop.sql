USE OrderPerf;
SET NOCOUNT ON;

DECLARE @batch INT = 200;
DECLARE @olderThan DATETIMEOFFSET(7) = DATEADD(DAY, -7, SYSDATETIMEOFFSET());
DECLARE @deleted INT = @batch;
DECLARE @no INT = 0;
DECLARE @before BIGINT, @after BIGINT;
DECLARE @at DATETIME2;

CREATE TABLE #log (batch_no INT, deleted INT, reads BIGINT, ms INT);

WHILE @deleted = @batch
BEGIN
    SELECT @before = logical_reads FROM sys.dm_exec_requests WHERE session_id = @@SPID;
    SET @at = SYSDATETIME();

    EXEC sp_executesql N'
    DELETE TOP(@p0) FROM [o]
    FROM [messaging].[outbox] AS [o]
    WHERE [o].[status] = ''Completed'' AND [o].[created_at] < @p1',
        N'@p0 int, @p1 datetimeoffset(7)',
        @p0 = @batch, @p1 = @olderThan;

    SET @deleted = @@ROWCOUNT;
    SELECT @after = logical_reads FROM sys.dm_exec_requests WHERE session_id = @@SPID;
    SET @no += 1;
    INSERT #log VALUES (@no, @deleted, @after - @before, DATEDIFF(MILLISECOND, @at, SYSDATETIME()));
END;

SELECT @batch AS batch_size, COUNT(*) AS batches, SUM(deleted) AS deleted, SUM(reads) AS reads, SUM(ms) AS total_ms
FROM #log;

WITH t AS (SELECT *, NTILE(10) OVER (ORDER BY batch_no) AS tenth FROM #log)
SELECT tenth, MIN(batch_no) AS first_batch, MAX(batch_no) AS last_batch,
       AVG(reads) AS avg_reads, MAX(reads) AS max_reads, AVG(ms) AS avg_ms
FROM t
GROUP BY tenth
ORDER BY tenth;

SELECT i.name AS index_name, s.record_count, s.ghost_record_count, s.version_ghost_record_count
FROM sys.dm_db_index_physical_stats(DB_ID(), OBJECT_ID('messaging.outbox'), NULL, NULL, 'DETAILED') AS s
JOIN sys.indexes AS i ON i.object_id = s.object_id AND i.index_id = s.index_id
WHERE s.index_level = 0;

DROP TABLE #log;
