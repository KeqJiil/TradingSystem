USE StockPerf;
SET NOCOUNT ON;
SET QUOTED_IDENTIFIER ON;

DECLARE @stocks INT = 100;
DECLARE @hot INT = 3;
DECLARE @hotHistory INT = 1000000;
DECLARE @hotHour INT = 360000;
DECLARE @coldHistory INT = 5000;
DECLARE @coldHour INT = 50;
DECLARE @days INT = 7;
DECLARE @batch INT = 200000;
DECLARE @hourStart DATETIMEOFFSET = DATEADD(HOUR, -1, DATETRUNC(HOUR, SYSDATETIMEOFFSET()));
DECLARE @historyStart DATETIMEOFFSET = DATEADD(DAY, -@days, @hourStart);

TRUNCATE TABLE events_store;
TRUNCATE TABLE stock_data;
TRUNCATE TABLE stock_data_projection;
TRUNCATE TABLE hourly_stock_data_projection;
TRUNCATE TABLE daily_stock_data_projection;

INSERT stock_data (id, name, is_open_to_trade, trading_start_time, trading_end_time, currency)
SELECT CONVERT(UNIQUEIDENTIFIER, HASHBYTES('MD5', CONCAT('stock-', s.value))),
       CONCAT(IIF(s.value <= @hot, 'HOT', 'COLD'), s.value),
       1, '00:00', '23:59:59', 'USD'
FROM GENERATE_SERIES(1, @stocks) AS s;

DECLARE @no INT = 1;
DECLARE @id UNIQUEIDENTIFIER;
DECLARE @history INT;
DECLARE @total INT;
DECLARE @from INT;

WHILE @no <= @stocks
BEGIN
    SELECT @id = id FROM stock_data WHERE name = CONCAT(IIF(@no <= @hot, 'HOT', 'COLD'), @no);
    SET @history = IIF(@no <= @hot, @hotHistory, @coldHistory);
    SET @total = @history + IIF(@no <= @hot, @hotHour, @coldHour);
    SET @from = 1;

    WHILE @from <= @total
    BEGIN
        INSERT events_store (event_id, aggregate_id, version, event_type, payload, price_change, created_at, occured_at)
        SELECT NEWID(),
               @id,
               g.value,
               N'PriceChangedEvent',
               CONCAT(N'{"AggregateId":"', @id, N'","PriceChange":', (g.value % 21 - 10) / 1000.0, N',"Version":', g.value, N'}'),
               (g.value % 21 - 10) / 1000.0,
               t.occured_at,
               t.occured_at
        FROM GENERATE_SERIES(@from, LEAST(@from + @batch - 1, @total)) AS g
        CROSS APPLY (SELECT IIF(g.value <= @history,
                                DATEADD(MILLISECOND, CAST((g.value - 1) * (@days * 86400000.0 / @history) AS INT), @historyStart),
                                DATEADD(MILLISECOND, CAST((g.value - @history - 1) * (3600000.0 / (@total - @history)) AS INT), @hourStart)) AS occured_at) AS t
        ORDER BY g.value;

        CHECKPOINT;
        SET @from += @batch;
    END;

    SET @no += 1;
END;

INSERT stock_data_projection (aggregate_id, name, is_open_to_trade, price, version, trading_start_time, trading_end_time, currency)
SELECT s.id, s.name, 1, 100 + e.price, e.version, '00:00', '23:59:59', 'USD'
FROM stock_data AS s
CROSS APPLY (SELECT SUM(price_change) AS price, MAX(version) AS version FROM events_store WHERE aggregate_id = s.id) AS e;

INSERT hourly_stock_data_projection (id, aggregate_id, open_price, low_price, high_price, close_price, price_difference, last_version, date_time)
SELECT NEWID(), s.id, 100, 99, 101, 100, 0, 0, DATEADD(HOUR, -h.value, @hourStart)
FROM stock_data AS s
CROSS JOIN GENERATE_SERIES(1, @days * 24) AS h;

INSERT daily_stock_data_projection (id, aggregate_id, open_price, low_price, high_price, close_price, price_difference, last_version, date)
SELECT NEWID(), s.id, 100, 99, 101, 100, 0, 0, DATEADD(DAY, -d.value, CAST(@hourStart AS DATE))
FROM stock_data AS s
CROSS JOIN GENERATE_SERIES(1, @days) AS d;

SELECT LEFT(s.name, IIF(s.name LIKE 'HOT%', 3, 4)) AS kind,
       COUNT(DISTINCT s.id) AS stocks,
       COUNT(*) AS events,
       SUM(IIF(e.occured_at >= @hourStart, 1, 0)) AS in_hour,
       @hourStart AS hour_start
FROM events_store AS e
JOIN stock_data AS s ON s.id = e.aggregate_id
GROUP BY LEFT(s.name, IIF(s.name LIKE 'HOT%', 3, 4));
