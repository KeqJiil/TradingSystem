USE OrderPerf;
SET NOCOUNT ON;
SET QUOTED_IDENTIFIER ON;

DECLARE @stocks INT = 300;
DECLARE @hotOrders INT = 100000;
DECLARE @users INT = 10000;
DECLARE @perUser INT = 20;
DECLARE @days INT = 365;
DECLARE @end DATETIMEOFFSET = TODATETIMEOFFSET(DATETRUNC(MINUTE, SYSUTCDATETIME()), 0);
DECLARE @epoch DATETIMEOFFSET = '1970-01-01T00:00:00+00:00';
DECLARE @hotUser UNIQUEIDENTIFIER = CONVERT(UNIQUEIDENTIFIER, HASHBYTES('MD5', 'user-hot'));

TRUNCATE TABLE [order].[fill];
DELETE [order].[order];
TRUNCATE TABLE [order].[stock];

INSERT [order].[stock] (stock_id, name, is_open_to_trade, trading_start, trading_end)
SELECT CONVERT(UNIQUEIDENTIFIER, HASHBYTES('MD5', CONCAT('stock-', s.value))),
       CONCAT('STK', FORMAT(s.value, '000')),
       1, '00:00', '23:59:59.9999999'
FROM GENERATE_SERIES(1, @stocks) AS s;

INSERT [order].[order] (id, user_id, stock_id, quantity, filled_quantity, order_side, status,
                        limit_price, time_in_force, order_type,
                        approved_at, rejected_at, rejection_reason, canceled_at, cancellation_reason,
                        expired_at, filled_at, created_at)
SELECT CONVERT(UNIQUEIDENTIFIER, CONCAT(LEFT(v.ts, 8), '-', RIGHT(v.ts, 4), '-7', SUBSTRING(v.r, 16, 3),
                                        '-8', SUBSTRING(v.r, 21, 3), '-', RIGHT(v.r, 12))),
       t.user_id,
       CONVERT(UNIQUEIDENTIFIER, HASHBYTES('MD5', CONCAT('stock-', g.value * 37 % @stocks + 1))),
       q.quantity,
       q.filled,
       IIF(g.value % 2 = 0, N'Buy', N'Sell'),
       st.status,
       IIF(ty.is_limit = 1, CAST(10 + g.value * 13 % 49000 / 100.0 AS DECIMAL(18, 2)), NULL),
       IIF(ty.is_limit = 0, N'Ioc', IIF(st.active = 0 AND g.value % 3 = 0, N'Day', N'Gtc')),
       IIF(ty.is_limit = 1, N'Limit', N'Market'),
       IIF(st.status IN (N'PendingNew', N'Rejected'), NULL, DATEADD(MILLISECOND, 50, t.created_at)),
       IIF(st.status = N'Rejected', DATEADD(MILLISECOND, 50, t.created_at), NULL),
       IIF(st.status = N'Rejected', N'Insufficient buying power.', NULL),
       IIF(st.status = N'Canceled', DATEADD(MINUTE, 5, t.created_at), NULL),
       IIF(st.status = N'Canceled', N'user request', NULL),
       IIF(st.status = N'Expired', DATEADD(HOUR, 8, t.created_at), NULL),
       IIF(st.status = N'Filled', DATEADD(SECOND, 5, t.created_at), NULL),
       t.created_at
FROM GENERATE_SERIES(1, @hotOrders + @users * @perUser) AS g
CROSS APPLY (SELECT IIF(g.value <= @hotOrders, 1, 0) AS hot,
                    CAST(CAST(g.value AS BIGINT) * 7919 % 1000 AS INT) AS bucket) AS b
CROSS APPLY (SELECT IIF(b.hot = 1, @hotUser,
                        CONVERT(UNIQUEIDENTIFIER, HASHBYTES('MD5', CONCAT('user-', (g.value - @hotOrders - 1) % @users + 1))))
                        AS user_id,
                    DATEADD(MILLISECOND, -(g.value % 1000),
                            DATEADD(SECOND, -CAST(IIF(b.hot = 1, g.value - 1, g.value - @hotOrders - 1)
                                                  * (@days * 86400.0 / IIF(b.hot = 1, @hotOrders, @users * @perUser)) AS INT),
                                    @end)) AS created_at) AS t
CROSS APPLY (SELECT CASE
                        WHEN b.bucket < 550 THEN N'Filled'
                        WHEN b.bucket < 800 THEN N'Canceled'
                        WHEN b.bucket < 890 THEN N'Expired'
                        WHEN b.bucket < 970 THEN N'Rejected'
                        WHEN b.bucket < 990 THEN N'New'
                        WHEN b.bucket < 995 THEN N'PartiallyFilled'
                        WHEN b.bucket < 998 THEN N'PendingNew'
                        ELSE N'PendingCancel'
                    END AS status,
                    IIF(b.bucket >= 970, 1, 0) AS active) AS st
CROSS APPLY (SELECT IIF(st.active = 1 OR g.value % 10 < 7, 1, 0) AS is_limit) AS ty
CROSS APPLY (SELECT 2 + g.value * 31 % 99 AS quantity) AS qb
CROSS APPLY (SELECT qb.quantity,
                    CASE
                        WHEN st.status = N'Filled' THEN qb.quantity
                        WHEN st.status = N'PartiallyFilled' THEN qb.quantity / 2
                        WHEN st.status IN (N'Canceled', N'Expired') AND b.bucket % 3 = 0 THEN qb.quantity / 2
                        ELSE 0
                    END AS filled) AS q
CROSS APPLY (SELECT RIGHT(CONVERT(VARCHAR(16), CONVERT(VARBINARY(8), DATEDIFF_BIG(MILLISECOND, @epoch, t.created_at)), 2), 12) AS ts,
                    CONVERT(CHAR(36), NEWID()) AS r) AS v;

INSERT [order].[fill] (order_id, execution_id, quantity, price, filled_at)
SELECT o.id,
       NEWID(),
       IIF(s.value = c.cnt, o.filled_quantity - (c.cnt - 1) * (o.filled_quantity / c.cnt), o.filled_quantity / c.cnt),
       ISNULL(o.limit_price, 100),
       DATEADD(SECOND, s.value, o.created_at)
FROM [order].[order] AS o
CROSS APPLY (SELECT LEAST(o.filled_quantity, 1 + ABS(CHECKSUM(o.id) % 5)) AS cnt) AS c
CROSS APPLY GENERATE_SERIES(1, c.cnt) AS s
WHERE o.filled_quantity > 0;

UPDATE STATISTICS [order].[order] WITH FULLSCAN;
UPDATE STATISTICS [order].[fill] WITH FULLSCAN;
CHECKPOINT;

SELECT IIF(o.user_id = @hotUser, 'hot', 'background') AS kind,
       COUNT(DISTINCT o.user_id) AS users,
       COUNT(*) AS orders,
       SUM(f.fills) AS fills,
       MIN(o.created_at) AS oldest,
       MAX(o.created_at) AS newest
FROM [order].[order] AS o
CROSS APPLY (SELECT COUNT(*) AS fills FROM [order].[fill] WHERE order_id = o.id) AS f
GROUP BY IIF(o.user_id = @hotUser, 'hot', 'background');

SELECT status, COUNT(*) AS orders
FROM [order].[order]
WHERE user_id = @hotUser
GROUP BY status
ORDER BY orders DESC;
