USE OrderPerf;
SET NOCOUNT ON;

DECLARE @page INT = 0;
DECLARE @limit INT = 20;
DECLARE @status NVARCHAR(20) = NULL;
DECLARE @user UNIQUEIDENTIFIER = CONVERT(UNIQUEIDENTIFIER, HASHBYTES('MD5', 'user-hot'));
DECLARE @afterCreatedAt DATETIMEOFFSET(7);
DECLARE @afterId UNIQUEIDENTIFIER;

IF @page > 0
    SELECT @afterCreatedAt = created_at, @afterId = id
    FROM [order].[order]
    WHERE user_id = @user AND (@status IS NULL OR status = @status)
    ORDER BY created_at DESC, id DESC
    OFFSET @page * @limit - 1 ROWS FETCH NEXT 1 ROWS ONLY;

DECLARE @sql NVARCHAR(MAX) = N'SELECT [s0].[id], [s0].[approved_at], [s0].[canceled_at], [s0].[cancellation_reason], [s0].[created_at], [s0].[expired_at], [s0].[filled_at], [s0].[filled_quantity], [s0].[order_side], [s0].[quantity], [s0].[rejected_at], [s0].[rejection_reason], [s0].[status], [s0].[stock_id], [s0].[user_id], [s0].[row_version], [s0].[limit_price], [s0].[time_in_force], [s0].[order_type], [s0].[stock_id0], [f].[id], [f].[execution_id], [f].[filled_at], [f].[order_id], [f].[price], [f].[quantity], [s0].[name]
FROM (
    SELECT TOP(@p) [o].[id], [o].[approved_at], [o].[canceled_at], [o].[cancellation_reason], [o].[created_at], [o].[expired_at], [o].[filled_at], [o].[filled_quantity], [o].[order_side], [o].[quantity], [o].[rejected_at], [o].[rejection_reason], [o].[status], [o].[stock_id], [o].[user_id], [o].[row_version], [o].[limit_price], [o].[time_in_force], [o].[order_type], [s].[name], [s].[stock_id] AS [stock_id0]
    FROM [order].[order] AS [o]
    LEFT JOIN [order].[stock] AS [s] ON [o].[stock_id] = [s].[stock_id]
    WHERE [o].[user_id] = @userId'
    + IIF(@status IS NULL, N'', N' AND [o].[status] = @status_Value')
    + IIF(@afterId IS NULL, N'', N' AND ([o].[created_at] < @afterCreatedAt_Value OR ([o].[created_at] = @afterCreatedAt_Value AND [o].[id] < @afterId_Value))')
    + N'
    ORDER BY [o].[created_at] DESC, [o].[id] DESC
) AS [s0]
LEFT JOIN [order].[fill] AS [f] ON [s0].[id] = [f].[order_id]
ORDER BY [s0].[created_at] DESC, [s0].[id] DESC, [s0].[stock_id0]';

SET STATISTICS PROFILE ON;
SET STATISTICS IO, TIME ON;

EXEC sp_executesql @sql,
    N'@p int, @userId uniqueidentifier, @status_Value nvarchar(20), @afterCreatedAt_Value datetimeoffset(7), @afterId_Value uniqueidentifier',
    @p = @limit, @userId = @user, @status_Value = @status,
    @afterCreatedAt_Value = @afterCreatedAt, @afterId_Value = @afterId;
