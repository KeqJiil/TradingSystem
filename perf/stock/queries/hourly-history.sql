USE StockPerf;
SET NOCOUNT ON;

DECLARE @stock UNIQUEIDENTIFIER = (SELECT id FROM stock_data WHERE name = 'HOT1');
DECLARE @hourStart DATETIMEOFFSET(7) = DATETRUNC(HOUR, (SELECT MAX(occured_at) FROM events_store WHERE aggregate_id = @stock));
DECLARE @weekAgo DATETIMEOFFSET(7) = DATEADD(DAY, -7, @hourStart);

SET STATISTICS PROFILE ON;
SET STATISTICS IO, TIME ON;

EXEC sp_executesql N'SELECT
    r.aggregate_id AS StockId,
    r.open_price AS OpenPrice,
    r.low_price AS LowPrice,
    r.high_price AS HighPrice,
    r.close_price AS ClosePrice,
    r.date_time DateTime
FROM hourly_stock_data_projection AS r
WHERE r.aggregate_id = @StockId
  AND r.date_time >= @from
  AND r.date_time < @to
ORDER BY r.date_time ASC',
    N'@StockId uniqueidentifier, @from datetimeoffset(7), @to datetimeoffset(7)',
    @StockId = @stock, @from = @weekAgo, @to = @hourStart;

EXEC sp_executesql N'SELECT TOP 1
    r.aggregate_id AS StockId,
    r.open_price AS OpenPrice,
    r.low_price AS LowPrice,
    r.high_price AS HighPrice,
    r.close_price AS ClosePrice,
    r.date_time DateTime
FROM hourly_stock_data_projection AS r
WHERE r.aggregate_id = @StockId
  AND r.date_time < @before
ORDER BY r.date_time DESC',
    N'@StockId uniqueidentifier, @before datetimeoffset(7)',
    @StockId = @stock, @before = @hourStart;
