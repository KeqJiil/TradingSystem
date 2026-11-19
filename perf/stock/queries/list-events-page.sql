USE StockPerf;
SET NOCOUNT ON;

DECLARE @page INT = 0;
DECLARE @stock UNIQUEIDENTIFIER = (SELECT id FROM stock_data WHERE name = 'HOT1');
DECLARE @hourStart DATETIMEOFFSET(7) = DATETRUNC(HOUR, (SELECT MAX(occured_at) FROM events_store WHERE aggregate_id = @stock));
DECLARE @hourEnd DATETIMEOFFSET(7) = DATEADD(HOUR, 1, @hourStart);
DECLARE @lastOccuredAt DATETIMEOFFSET(7);
DECLARE @lastVersion BIGINT;

IF @page > 0
    SELECT @lastOccuredAt = occured_at, @lastVersion = version
    FROM events_store
    WHERE aggregate_id = @stock AND occured_at >= @hourStart AND occured_at < @hourEnd
    ORDER BY occured_at, version
    OFFSET @page * 200 - 1 ROWS FETCH NEXT 1 ROWS ONLY;

SET STATISTICS PROFILE ON;
SET STATISTICS IO, TIME ON;

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
