USE OrderPerf;
SET STATISTICS PROFILE ON;
SET STATISTICS IO, TIME ON;

BEGIN TRAN;

EXEC sp_executesql N'
;WITH cte AS (
    SELECT TOP(@p0) o.*
    FROM messaging.outbox AS o WITH (ROWLOCK, UPDLOCK, READPAST)
    WHERE o.status IN (''Pending'', ''Processing'')
        AND (o.processed_at IS NULL OR o.processed_at <= DATEADD(MINUTE, -@p1, SYSDATETIMEOFFSET()))
        AND NOT EXISTS (
            SELECT 1
            FROM messaging.outbox AS earlier
            WHERE earlier.topic = o.topic
                AND earlier.message_key = o.message_key
                AND earlier.id < o.id
                AND earlier.status IN (''PENDING'', ''PROCESSING''))
    ORDER BY o.id
)
UPDATE cte
SET status = ''Processing'', processed_at = SYSDATETIMEOFFSET(),
    attempts = cte.attempts + 1
OUTPUT
    inserted.*',
    N'@p0 int, @p1 int',
    @p0 = 50, @p1 = 2;

ROLLBACK;
