USE OrderPerf;
SET NOCOUNT ON;
SET QUOTED_IDENTIFIER ON;

ALTER DATABASE CURRENT SET RECOVERY SIMPLE;

DECLARE @rows INT = 1000000;
DECLARE @pending INT = 10000;
DECLARE @keys INT = 200000;
DECLARE @batch INT = 100000;
DECLARE @now DATETIMEOFFSET = SYSDATETIMEOFFSET();

TRUNCATE TABLE messaging.outbox;

DECLARE @from INT = 1;
WHILE @from <= @rows
BEGIN
    INSERT messaging.outbox (id, message_id, message_type, topic, message_key, payload, headers, attempts, status, created_at, processed_at)
    SELECT NEXT VALUE FOR messaging.outbox_id_seq OVER (ORDER BY g.value),
           CONVERT(VARCHAR(36), NEWID()),
           CHOOSE(1 + g.value % 4, 'OrderPlaced', 'OrderApproved', 'FillRecorded', 'OrderFilled'),
           'order-events-topic',
           CONVERT(VARCHAR(36), CONVERT(UNIQUEIDENTIFIER, HASHBYTES('MD5', CONVERT(VARBINARY(4), g.value % @keys)))),
           CRYPT_GEN_RANDOM(200),
           CONCAT(N'{"correlation-id":"', NEWID(), N'","traceparent":"00-', REPLACE(CONVERT(CHAR(36), NEWID()), '-', ''), N'-01"}'),
           IIF(g.value > @rows - @pending, 0, 1),
           IIF(g.value > @rows - @pending, 'Pending', 'Completed'),
           c.created_at,
           IIF(g.value > @rows - @pending, NULL, DATEADD(MILLISECOND, 50, c.created_at))
    FROM GENERATE_SERIES(@from, LEAST(@from + @batch - 1, @rows)) AS g
    CROSS APPLY (SELECT DATEADD(SECOND, g.value - @rows, @now) AS created_at) AS c
    ORDER BY g.value;

    CHECKPOINT;
    SET @from += @batch;
END;

SELECT status, COUNT(*) AS rows, MIN(id) AS min_id, MAX(id) AS max_id
FROM messaging.outbox
GROUP BY status;
