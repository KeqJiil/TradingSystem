USE OrderPerf;
SET NOCOUNT ON;
SET QUOTED_IDENTIFIER ON;

DECLARE @orderUserId BIT = 1;
DECLARE @orderUserStatus BIT = 1;
DECLARE @orderStatusPartial BIT = 1;
DECLARE @fillOrderId BIT = 1;

DECLARE @wanted TABLE (table_name SYSNAME, index_name SYSNAME, enabled BIT);
INSERT @wanted VALUES
    (N'[order].[order]', N'order_user_id_idx', @orderUserId),
    (N'[order].[order]', N'order_user_status_idx', @orderUserStatus),
    (N'[order].[order]', N'order_status_partial_idx', @orderStatusPartial),
    (N'[order].[fill]', N'IX_fill_order_id', @fillOrderId);

DECLARE @sql NVARCHAR(MAX) = N'';
SELECT @sql += N'ALTER INDEX ' + QUOTENAME(w.index_name) + N' ON ' + w.table_name
               + IIF(w.enabled = 1, N' REBUILD;', N' DISABLE;') + NCHAR(10)
FROM @wanted AS w
JOIN sys.indexes AS i ON i.object_id = OBJECT_ID(w.table_name) AND i.name = w.index_name
WHERE i.is_disabled = w.enabled;

EXEC sp_executesql @sql;
ALTER DATABASE SCOPED CONFIGURATION CLEAR PROCEDURE_CACHE;

SELECT OBJECT_NAME(i.object_id) AS table_name, i.name AS index_name, i.is_disabled
FROM sys.indexes AS i
WHERE i.object_id IN (OBJECT_ID(N'[order].[order]'), OBJECT_ID(N'[order].[fill]')) AND i.type = 2
ORDER BY table_name, index_name;
