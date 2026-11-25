USE OrderPerf;
SET NOCOUNT ON;

DECLARE @limit INT = 20;
DECLARE @hot UNIQUEIDENTIFIER = CONVERT(UNIQUEIDENTIFIER, HASHBYTES('MD5', 'user-hot'));
DECLARE @small UNIQUEIDENTIFIER = CONVERT(UNIQUEIDENTIFIER, HASHBYTES('MD5', 'user-1'));

DECLARE @runs TABLE (step INT, clear_cache BIT, user_name VARCHAR(10), user_id UNIQUEIDENTIFIER, status NVARCHAR(20));
INSERT @runs VALUES
    (1, 1, 'hot', @hot, N'PartiallyFilled'),
    (2, 0, 'hot', @hot, N'Filled'),
    (3, 0, 'small', @small, N'Filled'),
    (4, 1, 'hot', @hot, N'Filled'),
    (5, 0, 'hot', @hot, N'PartiallyFilled'),
    (6, 0, 'small', @small, N'PartiallyFilled'),
    (7, 1, 'hot', @hot, NULL);

DECLARE @withStatus NVARCHAR(MAX) = N'SELECT TOP(@p) [o].[id], [o].[stock_id], [s].[name], [o].[order_side], [o].[order_type], [o].[limit_price], [o].[quantity], [o].[filled_quantity], [o].[status], [o].[created_at]
FROM [order].[order] AS [o]
LEFT JOIN [order].[stock] AS [s] ON [o].[stock_id] = [s].[stock_id]
WHERE [o].[user_id] = @userId AND [o].[status] = @status_Value
ORDER BY [o].[created_at] DESC, [o].[id] DESC';
DECLARE @withoutStatus NVARCHAR(MAX) = N'SELECT TOP(@p) [o].[id], [o].[stock_id], [s].[name], [o].[order_side], [o].[order_type], [o].[limit_price], [o].[quantity], [o].[filled_quantity], [o].[status], [o].[created_at]
FROM [order].[order] AS [o]
LEFT JOIN [order].[stock] AS [s] ON [o].[stock_id] = [s].[stock_id]
WHERE [o].[user_id] = @userId
ORDER BY [o].[created_at] DESC, [o].[id] DESC';

CREATE TABLE #page (
    id UNIQUEIDENTIFIER, stock_id UNIQUEIDENTIFIER, name NVARCHAR(20), order_side NVARCHAR(20),
    order_type NVARCHAR(20), limit_price DECIMAL(18, 2), quantity INT, filled_quantity INT,
    status NVARCHAR(20), created_at DATETIMEOFFSET);
CREATE TABLE #log (step INT, compiled_for NVARCHAR(20), user_name VARCHAR(10), status NVARCHAR(20),
                   orders INT, reads BIGINT, ms INT);

DECLARE @step INT = 1, @steps INT = (SELECT MAX(step) FROM @runs);
DECLARE @clear BIT, @userName VARCHAR(10), @user UNIQUEIDENTIFIER, @status NVARCHAR(20), @compiledFor NVARCHAR(20);
DECLARE @before BIGINT, @after BIGINT, @at DATETIME2;

WHILE @step <= @steps
BEGIN
    SELECT @clear = clear_cache, @userName = user_name, @user = user_id, @status = status FROM @runs WHERE step = @step;

    IF @clear = 1
    BEGIN
        ALTER DATABASE SCOPED CONFIGURATION CLEAR PROCEDURE_CACHE;
        SET @compiledFor = ISNULL(@status, N'-');
    END;

    TRUNCATE TABLE #page;
    SELECT @before = logical_reads FROM sys.dm_exec_requests WHERE session_id = @@SPID;
    SET @at = SYSDATETIME();

    IF @status IS NULL
        INSERT #page
        EXEC sp_executesql @withoutStatus, N'@p int, @userId uniqueidentifier', @p = @limit, @userId = @user;
    ELSE
        INSERT #page
        EXEC sp_executesql @withStatus, N'@p int, @userId uniqueidentifier, @status_Value nvarchar(20)',
            @p = @limit, @userId = @user, @status_Value = @status;

    SELECT @after = logical_reads FROM sys.dm_exec_requests WHERE session_id = @@SPID;
    INSERT #log SELECT @step, @compiledFor, @userName, ISNULL(@status, N'-'), COUNT(*), @after - @before,
                       DATEDIFF(MILLISECOND, @at, SYSDATETIME())
                FROM #page;

    SET @step += 1;
END;

SELECT OBJECT_NAME(object_id) AS table_name, name AS index_name, is_disabled
FROM sys.indexes
WHERE object_id IN (OBJECT_ID(N'[order].[order]'), OBJECT_ID(N'[order].[fill]')) AND type = 2
ORDER BY table_name, index_name;

SELECT * FROM #log ORDER BY step;

DROP TABLE #page;
DROP TABLE #log;
