USE StockPerf;
SET NOCOUNT ON;

DECLARE @stock UNIQUEIDENTIFIER = (SELECT id FROM stock_data WHERE name = 'HOT1');
DECLARE @hourStart DATETIMEOFFSET(7) = DATETRUNC(HOUR, (SELECT MAX(occured_at) FROM events_store WHERE aggregate_id = @stock));
DECLARE @hourEnd DATETIMEOFFSET(7) = DATEADD(HOUR, 1, @hourStart);

SET STATISTICS PROFILE ON;
SET STATISTICS IO, TIME ON;

EXEC sp_executesql N'WITH running AS (
    SELECT s.version,
           s.price_change,
           SUM(s.price_change) OVER (ORDER BY s.occured_at, s.version ROWS UNBOUNDED PRECEDING) AS change,
           ROW_NUMBER() OVER (ORDER BY s.occured_at, s.version) AS rn
    FROM events_store s
    WHERE s.aggregate_id = @AggregateId
      AND s.occured_at >= @From AND s.occured_at < @To
)
SELECT COUNT(*) AS Events,
       MAX(IIF(rn = 1, change, NULL)) AS OpenChange,
       MAX(change) AS HighChange,
       MIN(change) AS LowChange,
       SUM(price_change) AS CloseChange,
       MAX(version) AS LastVersion
FROM running',
    N'@AggregateId uniqueidentifier, @From datetimeoffset(7), @To datetimeoffset(7)',
    @AggregateId = @stock, @From = @hourStart, @To = @hourEnd;
