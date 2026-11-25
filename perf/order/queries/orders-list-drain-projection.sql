USE OrderPerf;
SET NOCOUNT ON;

DECLARE @maxPages INT = 5000;
DECLARE @limit INT = 20;
DECLARE @status NVARCHAR(20) = NULL;
DECLARE @user UNIQUEIDENTIFIER = CONVERT(UNIQUEIDENTIFIER, HASHBYTES('MD5', 'user-hot'));
DECLARE @afterCreatedAt DATETIMEOFFSET(7);
DECLARE @afterId UNIQUEIDENTIFIER;
DECLARE @orders INT = 1;
DECLARE @no INT = 0;
DECLARE @before BIGINT, @after BIGINT;
DECLARE @at DATETIME2;
DECLARE @sql NVARCHAR(MAX);

DECLARE @head NVARCHAR(MAX) = N'SELECT TOP(@p) [o].[id], [o].[stock_id], [s].[name], [o].[order_side], [o].[order_type], [o].[limit_price], [o].[quantity], [o].[filled_quantity], [o].[status], [o].[created_at]
FROM [order].[order] AS [o]
LEFT JOIN [order].[stock] AS [s] ON [o].[stock_id] = [s].[stock_id]
WHERE [o].[user_id] = @userId'
    + IIF(@status IS NULL, N'', N' AND [o].[status] = @status_Value');
DECLARE @tail NVARCHAR(MAX) = N'
ORDER BY [o].[created_at] DESC, [o].[id] DESC';
DECLARE @cursor NVARCHAR(MAX) = N' AND ([o].[created_at] < @afterCreatedAt_Value OR ([o].[created_at] = @afterCreatedAt_Value AND [o].[id] < @afterId_Value))';

CREATE TABLE #page (
    id UNIQUEIDENTIFIER, stock_id UNIQUEIDENTIFIER, name NVARCHAR(20), order_side NVARCHAR(20),
    order_type NVARCHAR(20), limit_price DECIMAL(18, 2), quantity INT, filled_quantity INT,
    status NVARCHAR(20), created_at DATETIMEOFFSET);
CREATE TABLE #log (page_no INT, orders INT, rows INT, reads BIGINT, ms INT);

WHILE @orders > 0 AND @no < @maxPages
BEGIN
    TRUNCATE TABLE #page;
    SET @sql = @head + IIF(@afterId IS NULL, N'', @cursor) + @tail;
    SELECT @before = logical_reads FROM sys.dm_exec_requests WHERE session_id = @@SPID;
    SET @at = SYSDATETIME();

    INSERT #page
    EXEC sp_executesql @sql,
        N'@p int, @userId uniqueidentifier, @status_Value nvarchar(20), @afterCreatedAt_Value datetimeoffset(7), @afterId_Value uniqueidentifier',
        @p = @limit, @userId = @user, @status_Value = @status,
        @afterCreatedAt_Value = @afterCreatedAt, @afterId_Value = @afterId;

    SELECT @after = logical_reads FROM sys.dm_exec_requests WHERE session_id = @@SPID;
    SELECT @orders = COUNT(DISTINCT id) FROM #page;
    SET @no += 1;
    INSERT #log SELECT @no, @orders, COUNT(*), @after - @before, DATEDIFF(MILLISECOND, @at, SYSDATETIME()) FROM #page;

    SELECT TOP (1) @afterCreatedAt = created_at, @afterId = id FROM #page ORDER BY created_at, id;
END;

SELECT COUNT(*) AS pages, SUM(orders) AS orders, SUM(rows) AS rows, SUM(reads) AS reads, SUM(ms) AS total_ms
FROM #log;

WITH t AS (SELECT *, NTILE(10) OVER (ORDER BY page_no) AS tenth FROM #log WHERE orders > 0)
SELECT tenth, MIN(page_no) AS first_page, MAX(page_no) AS last_page,
       AVG(rows) AS avg_rows, AVG(reads) AS avg_reads, MAX(reads) AS max_reads, AVG(ms) AS avg_ms
FROM t
GROUP BY tenth
ORDER BY tenth;

DROP TABLE #page;
DROP TABLE #log;
