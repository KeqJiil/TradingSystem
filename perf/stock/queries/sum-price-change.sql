USE StockPerf;
SET NOCOUNT ON;

DECLARE @stock UNIQUEIDENTIFIER = (SELECT id FROM stock_data WHERE name = 'HOT1');
DECLARE @hourStart DATETIMEOFFSET(7) = DATETRUNC(HOUR, (SELECT MAX(occured_at) FROM events_store WHERE aggregate_id = @stock));
DECLARE @minValue DATETIMEOFFSET(7) = '0001-01-01 00:00:00 +00:00';

SET STATISTICS PROFILE ON;
SET STATISTICS IO, TIME ON;

EXEC sp_executesql N'SELECT COALESCE(SUM(price_change), 0)
FROM events_store
WHERE aggregate_id = @AggregateId AND occured_at >= @From AND occured_at < @To',
    N'@AggregateId uniqueidentifier, @From datetimeoffset(7), @To datetimeoffset(7)',
    @AggregateId = @stock, @From = @minValue, @To = @hourStart;
