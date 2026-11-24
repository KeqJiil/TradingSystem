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

DECLARE @sql NVARCHAR(MAX) = N'SELECT TOP(@p) [o].[id], [o].[stock_id], [s].[name], [o].[order_side], [o].[order_type], [o].[limit_price], [o].[quantity], [o].[filled_quantity], [o].[status], [o].[created_at]
FROM [order].[order] AS [o]
LEFT JOIN [order].[stock] AS [s] ON [o].[stock_id] = [s].[stock_id]
WHERE [o].[user_id] = @userId'
    + IIF(@status IS NULL, N'', N' AND [o].[status] = @status_Value')
    + IIF(@afterId IS NULL, N'', N' AND ([o].[created_at] < @afterCreatedAt_Value OR ([o].[created_at] = @afterCreatedAt_Value AND [o].[id] < @afterId_Value))')
    + N'
ORDER BY [o].[created_at] DESC, [o].[id] DESC';

SET STATISTICS PROFILE ON;
SET STATISTICS IO, TIME ON;

EXEC sp_executesql @sql,
    N'@p int, @userId uniqueidentifier, @status_Value nvarchar(20), @afterCreatedAt_Value datetimeoffset(7), @afterId_Value uniqueidentifier',
    @p = @limit, @userId = @user, @status_Value = @status,
    @afterCreatedAt_Value = @afterCreatedAt, @afterId_Value = @afterId;
