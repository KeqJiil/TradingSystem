USE StockPerf;
SET NOCOUNT ON;

DECLARE @stock UNIQUEIDENTIFIER = (SELECT id FROM stock_data WHERE name = 'HOT1');

SET STATISTICS PROFILE ON;
SET STATISTICS IO, TIME ON;

EXEC sp_executesql N'SELECT aggregate_id AS AggregateId, MAX(version) AS Version
FROM events_store
WHERE aggregate_id IN (@AggregateIds1)
GROUP BY aggregate_id',
    N'@AggregateIds1 uniqueidentifier',
    @AggregateIds1 = @stock;
