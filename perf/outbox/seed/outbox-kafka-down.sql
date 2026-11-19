USE OrderPerf;
SET NOCOUNT ON;
SET QUOTED_IDENTIFIER ON;

ALTER DATABASE CURRENT SET RECOVERY SIMPLE;

DECLARE @rows INT = 1000000;
DECLARE @unfinished INT = 110000;
DECLARE @blockedKeys INT = 10000;
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
           'OrderPlaced',
           'order-events-topic',
           CONVERT(VARCHAR(36), CONVERT(UNIQUEIDENTIFIER, HASHBYTES('MD5', CONVERT(VARBINARY(4), k.key_no)))),
           CONVERT(VARBINARY(200), REPLICATE('x', 200)),
           N'{"correlation-id":"00000000-0000-0000-0000-000000000000","traceparent":"00-00000000000000000000000000000000-0000000000000000-01"}',
           IIF(s.status = 'Pending', 0, 1),
           s.status,
           c.created_at,
           CASE s.status
               WHEN 'Completed' THEN DATEADD(MILLISECOND, 50, c.created_at)
               WHEN 'Processing' THEN DATEADD(HOUR, 1, @now)
           END
    FROM GENERATE_SERIES(@from, LEAST(@from + @batch - 1, @rows)) AS g
    CROSS APPLY (SELECT DATEADD(SECOND, g.value - @rows, @now) AS created_at) AS c
    CROSS APPLY (SELECT g.value - (@rows - @unfinished) - 1 AS n) AS u
    CROSS APPLY (SELECT IIF(u.n < 0, g.value % @keys, @keys + u.n % @blockedKeys) AS key_no) AS k
    CROSS APPLY (SELECT CASE
                            WHEN u.n < 0 THEN 'Completed'
                            WHEN u.n < @blockedKeys THEN 'Processing'
                            ELSE 'Pending'
                        END AS status) AS s
    ORDER BY g.value;

    CHECKPOINT;
    SET @from += @batch;
END;

SELECT status, COUNT(*) AS rows, COUNT(DISTINCT message_key) AS keys, MIN(id) AS min_id, MAX(id) AS max_id
FROM messaging.outbox
GROUP BY status
ORDER BY min_id;
