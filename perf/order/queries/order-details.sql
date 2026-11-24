USE OrderPerf;
SET NOCOUNT ON;

DECLARE @user UNIQUEIDENTIFIER = CONVERT(UNIQUEIDENTIFIER, HASHBYTES('MD5', 'user-hot'));
DECLARE @order UNIQUEIDENTIFIER = (SELECT TOP (1) id FROM [order].[order]
                                   WHERE user_id = @user AND status = N'Filled'
                                   ORDER BY created_at DESC, id DESC);

SET STATISTICS PROFILE ON;
SET STATISTICS IO, TIME ON;

EXEC sp_executesql N'SELECT [s0].[id], [s0].[approved_at], [s0].[canceled_at], [s0].[cancellation_reason], [s0].[created_at], [s0].[expired_at], [s0].[filled_at], [s0].[filled_quantity], [s0].[order_side], [s0].[quantity], [s0].[rejected_at], [s0].[rejection_reason], [s0].[status], [s0].[stock_id], [s0].[user_id], [s0].[row_version], [s0].[limit_price], [s0].[time_in_force], [s0].[order_type], [s0].[stock_id0], [f].[id], [f].[execution_id], [f].[filled_at], [f].[order_id], [f].[price], [f].[quantity], [s0].[name]
FROM (
    SELECT TOP(1) [o].[id], [o].[approved_at], [o].[canceled_at], [o].[cancellation_reason], [o].[created_at], [o].[expired_at], [o].[filled_at], [o].[filled_quantity], [o].[order_side], [o].[quantity], [o].[rejected_at], [o].[rejection_reason], [o].[status], [o].[stock_id], [o].[user_id], [o].[row_version], [o].[limit_price], [o].[time_in_force], [o].[order_type], [s].[name], [s].[stock_id] AS [stock_id0]
    FROM [order].[order] AS [o]
    LEFT JOIN [order].[stock] AS [s] ON [o].[stock_id] = [s].[stock_id]
    WHERE [o].[id] = @orderId AND [o].[user_id] = @userId
) AS [s0]
LEFT JOIN [order].[fill] AS [f] ON [s0].[id] = [f].[order_id]
ORDER BY [s0].[id], [s0].[stock_id0]',
    N'@orderId uniqueidentifier, @userId uniqueidentifier',
    @orderId = @order, @userId = @user;

EXEC sp_executesql N'SELECT [o0].[id], [o0].[approved_at], [o0].[canceled_at], [o0].[cancellation_reason], [o0].[created_at], [o0].[expired_at], [o0].[filled_at], [o0].[filled_quantity], [o0].[order_side], [o0].[quantity], [o0].[rejected_at], [o0].[rejection_reason], [o0].[status], [o0].[stock_id], [o0].[user_id], [o0].[row_version], [o0].[limit_price], [o0].[time_in_force], [o0].[order_type], [f].[id], [f].[execution_id], [f].[filled_at], [f].[order_id], [f].[price], [f].[quantity]
FROM (
    SELECT TOP(1) [o].[id], [o].[approved_at], [o].[canceled_at], [o].[cancellation_reason], [o].[created_at], [o].[expired_at], [o].[filled_at], [o].[filled_quantity], [o].[order_side], [o].[quantity], [o].[rejected_at], [o].[rejection_reason], [o].[status], [o].[stock_id], [o].[user_id], [o].[row_version], [o].[limit_price], [o].[time_in_force], [o].[order_type]
    FROM [order].[order] AS [o]
    WHERE [o].[id] = @orderId
) AS [o0]
LEFT JOIN [order].[fill] AS [f] ON [o0].[id] = [f].[order_id]
ORDER BY [o0].[id]',
    N'@orderId uniqueidentifier',
    @orderId = @order;
