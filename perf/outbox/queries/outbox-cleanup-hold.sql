USE OrderPerf;
SET NOCOUNT ON;

DECLARE @batch INT = 2500;
DECLARE @olderThan DATETIMEOFFSET(7) = DATEADD(DAY, -7, SYSDATETIMEOFFSET());

BEGIN TRAN;

EXEC sp_executesql N'
DELETE TOP(@p0) FROM [o]
FROM [messaging].[outbox] AS [o]
WHERE [o].[status] = ''Completed'' AND [o].[created_at] < @p1',
    N'@p0 int, @p1 datetimeoffset(7)',
    @p0 = @batch, @p1 = @olderThan;

SELECT @@SPID AS session_id, @@ROWCOUNT AS deleted, @@TRANCOUNT AS open_tran;
