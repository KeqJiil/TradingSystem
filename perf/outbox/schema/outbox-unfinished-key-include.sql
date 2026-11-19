USE OrderPerf;
SET QUOTED_IDENTIFIER ON;

CREATE INDEX idx_outbox_unfinished_key
    ON messaging.outbox (topic, message_key, id)
    INCLUDE (processed_at)
    WHERE status IN ('Pending', 'Processing')
    WITH (DROP_EXISTING = ON);
