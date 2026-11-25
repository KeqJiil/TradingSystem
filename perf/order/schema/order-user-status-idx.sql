USE OrderPerf;
SET QUOTED_IDENTIFIER ON;

DROP INDEX IF EXISTS order_user_status_idx ON [order].[order];

CREATE INDEX order_user_status_idx
    ON [order].[order] (user_id DESC, status DESC, created_at DESC, id DESC);
