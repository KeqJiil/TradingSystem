USE StockPerf;
SET NOCOUNT ON;

DECLARE @maxPages INT = 2000;
DECLARE @stock UNIQUEIDENTIFIER = (SELECT id FROM stock_data WHERE name = 'HOT1');
DECLARE @hourStart DATETIMEOFFSET(7) = DATETRUNC(HOUR, (SELECT MAX(occured_at) FROM events_store WHERE aggregate_id = @stock));
DECLARE @hourEnd DATETIMEOFFSET(7) = DATEADD(HOUR, 1, @hourStart);
DECLARE @lastOccuredAt DATETIMEOFFSET(7);
DECLARE @lastVersion BIGINT;
DECLARE @rows INT = 1;
DECLARE @no INT = 0;
DECLARE @before BIGINT, @after BIGINT;
DECLARE @at DATETIME2;

CREATE TABLE #page (StockId UNIQUEIDENTIFIER, PriceChange DECIMAL(20, 4), Timestamp DATETIMEOFFSET(7), Version BIGINT);
CREATE TABLE #log (page_no INT, rows INT, reads BIGINT, ms INT);

WHILE @rows > 0 AND @no < @maxPages
BEGIN
    TRUNCATE TABLE #page;
    SELECT @before = logical_reads FROM sys.dm_exec_requests WHERE session_id = @@SPID;
    SET @at = SYSDATETIME();

    INSERT #page
    EXEC sp_executesql N'SELECT TOP (@PageSize) s.aggregate_id AS StockId, s.price_change AS PriceChange,
       s.occured_at AS Timestamp, s.version AS Version
FROM events_store s
WHERE s.aggregate_id = @AggregateId
  AND s.occured_at >= @From AND s.occured_at < @To
  AND (
    @LastOccuredAt IS NULL
    OR s.occured_at > @LastOccuredAt
    OR (s.occured_at = @LastOccuredAt AND s.version > @LastVersion)
  )
ORDER BY s.occured_at ASC, s.version ASC',
        N'@PageSize int, @AggregateId uniqueidentifier, @From datetimeoffset(7), @To datetimeoffset(7), @LastOccuredAt datetimeoffset(7), @LastVersion bigint',
        @PageSize = 200, @AggregateId = @stock, @From = @hourStart, @To = @hourEnd,
        @LastOccuredAt = @lastOccuredAt, @LastVersion = @lastVersion;

    SET @rows = @@ROWCOUNT;
    SELECT @after = logical_reads FROM sys.dm_exec_requests WHERE session_id = @@SPID;
    SET @no += 1;
    INSERT #log VALUES (@no, @rows, @after - @before, DATEDIFF(MILLISECOND, @at, SYSDATETIME()));

    SELECT TOP (1) @lastOccuredAt = Timestamp, @lastVersion = Version FROM #page ORDER BY Timestamp DESC, Version DESC;
END;

SELECT COUNT(*) AS pages, SUM(rows) AS rows, SUM(reads) AS reads, SUM(ms) AS total_ms
FROM #log;

WITH t AS (SELECT *, NTILE(10) OVER (ORDER BY page_no) AS tenth FROM #log WHERE rows > 0)
SELECT tenth, MIN(page_no) AS first_page, MAX(page_no) AS last_page,
       AVG(reads) AS avg_reads, AVG(ms) AS avg_ms
FROM t
GROUP BY tenth
ORDER BY tenth;

DROP TABLE #page;
DROP TABLE #log;
