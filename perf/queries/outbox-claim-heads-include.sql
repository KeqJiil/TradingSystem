USE OrderPerf;
SET STATISTICS PROFILE ON;
SET STATISTICS IO, TIME ON;

BEGIN TRAN;

EXEC sp_executesql N'
;WITH ranked AS (
    SELECT id, processed_at, ROW_NUMBER() OVER (PARTITION BY topic, message_key ORDER BY id) AS rn
    FROM messaging.outbox
    WHERE status IN (''Pending'', ''Processing'')
), heads AS (
    SELECT TOP(@p0) id
    FROM ranked
    WHERE rn = 1 AND (processed_at IS NULL OR processed_at <= DATEADD(MINUTE, -@p1, SYSDATETIMEOFFSET()))
    ORDER BY id
)
UPDATE o
SET status = ''Processing'', processed_at = SYSDATETIMEOFFSET(),
    attempts = o.attempts + 1
OUTPUT
    inserted.*
FROM messaging.outbox AS o WITH (ROWLOCK, UPDLOCK, READPAST)
JOIN heads AS h ON h.id = o.id
WHERE o.status IN (''Pending'', ''Processing'')
    AND (o.processed_at IS NULL OR o.processed_at <= DATEADD(MINUTE, -@p1, SYSDATETIMEOFFSET()))',
    N'@p0 int, @p1 int',
    @p0 = 50, @p1 = 2;

ROLLBACK;
