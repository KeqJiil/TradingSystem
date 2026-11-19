USE OrderPerf;

CREATE TABLE #claimed (id BIGINT, topic VARCHAR(255), message_key VARCHAR(200));

BEGIN TRAN;

INSERT #claimed
EXEC sp_executesql N'
;WITH cte AS (
    SELECT TOP(@p0) o.*
    FROM messaging.outbox AS o WITH (ROWLOCK, UPDLOCK, READPAST)
    WHERE o.status IN (''Pending'', ''Processing'')
        AND (o.processed_at IS NULL OR o.processed_at <= DATEADD(MINUTE, -@p1, SYSDATETIMEOFFSET()))
        AND o.id = (SELECT MIN(e.id) FROM messaging.outbox AS e
            WHERE e.topic = o.topic AND e.message_key = o.message_key
              AND e.status IN (''Pending'', ''Processing''))
    ORDER BY o.id
)
UPDATE cte
SET status = ''Processing'', processed_at = SYSDATETIMEOFFSET(),
    attempts = cte.attempts + 1
OUTPUT
    inserted.id, inserted.topic, inserted.message_key
OPTION (LOOP JOIN)',
    N'@p0 int, @p1 int',
    @p0 = 50, @p1 = 2;

SELECT COUNT(*) AS claimed,
       MIN(c.id) AS min_id,
       MAX(c.id) AS max_id,
       SUM(IIF(c.id = h.head_id, 0, 1)) AS not_heads
FROM #claimed AS c
CROSS APPLY (SELECT MIN(e.id) AS head_id
             FROM messaging.outbox AS e WITH (NOLOCK)
             WHERE e.topic = c.topic AND e.message_key = c.message_key
               AND e.status IN ('Pending', 'Processing')) AS h;

ROLLBACK;
DROP TABLE #claimed;
