USE StockPerf;
SET NOCOUNT ON;

DECLARE @hoursBack INT = 0;
DECLARE @stock UNIQUEIDENTIFIER = (SELECT id FROM stock_data WHERE name = 'HOT1');
DECLARE @before DATETIMEOFFSET(7) = DATEADD(HOUR, 1 - @hoursBack, DATETRUNC(HOUR, (SELECT MAX(occured_at) FROM events_store WHERE aggregate_id = @stock)));

SET STATISTICS PROFILE ON;
SET STATISTICS IO, TIME ON;

EXEC sp_executesql N'SELECT MAX(s.version)
FROM events_store s
WHERE s.aggregate_id = @AggregateId AND s.occured_at < @Before',
    N'@AggregateId uniqueidentifier, @Before datetimeoffset(7)',
    @AggregateId = @stock, @Before = @before;
