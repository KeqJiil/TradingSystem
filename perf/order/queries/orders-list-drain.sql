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

DECLARE @head NVARCHAR(MAX) = N'SELECT [s0].[id], [s0].[approved_at], [s0].[canceled_at], [s0].[cancellation_reason], [s0].[created_at], [s0].[expired_at], [s0].[filled_at], [s0].[filled_quantity], [s0].[order_side], [s0].[quantity], [s0].[rejected_at], [s0].[rejection_reason], [s0].[status], [s0].[stock_id], [s0].[user_id], [s0].[row_version], [s0].[limit_price], [s0].[time_in_force], [s0].[order_type], [s0].[stock_id0], [f].[id], [f].[execution_id], [f].[filled_at], [f].[order_id], [f].[price], [f].[quantity], [s0].[name]
FROM (
    SELECT TOP(@p) [o].[id], [o].[approved_at], [o].[canceled_at], [o].[cancellation_reason], [o].[created_at], [o].[expired_at], [o].[filled_at], [o].[filled_quantity], [o].[order_side], [o].[quantity], [o].[rejected_at], [o].[rejection_reason], [o].[status], [o].[stock_id], [o].[user_id], [o].[row_version], [o].[limit_price], [o].[time_in_force], [o].[order_type], [s].[name], [s].[stock_id] AS [stock_id0]
    FROM [order].[order] AS [o]
    LEFT JOIN [order].[stock] AS [s] ON [o].[stock_id] = [s].[stock_id]
    WHERE [o].[user_id] = @userId'
    + IIF(@status IS NULL, N'', N' AND [o].[status] = @status_Value');
DECLARE @tail NVARCHAR(MAX) = N'
    ORDER BY [o].[created_at] DESC, [o].[id] DESC
) AS [s0]
LEFT JOIN [order].[fill] AS [f] ON [s0].[id] = [f].[order_id]
ORDER BY [s0].[created_at] DESC, [s0].[id] DESC, [s0].[stock_id0]';
DECLARE @cursor NVARCHAR(MAX) = N' AND ([o].[created_at] < @afterCreatedAt_Value OR ([o].[created_at] = @afterCreatedAt_Value AND [o].[id] < @afterId_Value))';

CREATE TABLE #page (
    id UNIQUEIDENTIFIER, approved_at DATETIMEOFFSET, canceled_at DATETIMEOFFSET, cancellation_reason NVARCHAR(MAX),
    created_at DATETIMEOFFSET, expired_at DATETIMEOFFSET, filled_at DATETIMEOFFSET, filled_quantity INT,
    order_side NVARCHAR(20), quantity INT, rejected_at DATETIMEOFFSET, rejection_reason NVARCHAR(MAX),
    status NVARCHAR(20), stock_id UNIQUEIDENTIFIER, user_id UNIQUEIDENTIFIER, row_version BINARY(8),
    limit_price DECIMAL(18, 2), time_in_force NVARCHAR(20), order_type NVARCHAR(20), stock_id0 UNIQUEIDENTIFIER,
    fill_id BIGINT, execution_id UNIQUEIDENTIFIER, fill_filled_at DATETIMEOFFSET, order_id UNIQUEIDENTIFIER,
    price DECIMAL(18, 2), fill_quantity INT, name NVARCHAR(20));
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
