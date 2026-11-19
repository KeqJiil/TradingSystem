USE OrderPerf;
SET NOCOUNT ON;
SET QUOTED_IDENTIFIER ON;

ALTER DATABASE CURRENT SET RECOVERY SIMPLE;

DECLARE @rows INT = 1000000;
DECLARE @hot INT = 100000;
DECLARE @fresh INT = 10000;
DECLARE @keys INT = 200000;
DECLARE @batch INT = 100000;
DECLARE @hotKey VARCHAR(36) = CONVERT(VARCHAR(36), NEWID());
DECLARE @now DATETIMEOFFSET = SYSDATETIMEOFFSET();

TRUNCATE TABLE messaging.outbox;

DECLARE @from INT = 1;
WHILE @from <= @rows
BEGIN
    INSERT messaging.outbox (id, message_id, message_type, topic, message_key, payload, headers, attempts, status, created_at, processed_at)
    SELECT NEXT VALUE FOR messaging.outbox_id_seq OVER (ORDER BY g.value),
           CONVERT(VARCHAR(36), NEWID()),
           'OrderPlaced',
           'order-events-topic',
           IIF(k.kind = 'hot', @hotKey, CONVERT(VARCHAR(36), CONVERT(UNIQUEIDENTIFIER, HASHBYTES('MD5', CONVERT(VARBINARY(4), g.value % @keys))))),
           CONVERT(VARBINARY(200), REPLICATE('x', 200)),
           N'{"correlation-id":"00000000-0000-0000-0000-000000000000","traceparent":"00-00000000000000000000000000000000-0000000000000000-01"}',
           IIF(s.status = 'Pending', 0, 1),
           s.status,
           c.created_at,
           CASE s.status
               WHEN 'Completed' THEN DATEADD(MILLISECOND, 50, c.created_at)
               WHEN 'Processing' THEN @now
           END
    FROM GENERATE_SERIES(@from, LEAST(@from + @batch - 1, @rows)) AS g
    CROSS APPLY (SELECT DATEADD(SECOND, g.value - @rows, @now) AS created_at) AS c
    CROSS APPLY (SELECT CASE
                            WHEN g.value <= @rows - @hot - @fresh THEN 'done'
                            WHEN g.value <= @rows - @fresh THEN 'hot'
                            ELSE 'fresh'
                        END AS kind) AS k
    CROSS APPLY (SELECT CASE
                            WHEN k.kind = 'done' THEN 'Completed'
                            WHEN g.value = @rows - @hot - @fresh + 1 THEN 'Processing'
                            ELSE 'Pending'
                        END AS status) AS s
    ORDER BY g.value;

    CHECKPOINT;
    SET @from += @batch;
END;

SELECT status, IIF(message_key = @hotKey, 'hot', 'other') AS key_kind, COUNT(*) AS rows, MIN(id) AS min_id, MAX(id) AS max_id
FROM messaging.outbox
GROUP BY status, IIF(message_key = @hotKey, 'hot', 'other')
ORDER BY min_id;
