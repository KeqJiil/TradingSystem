IF NOT EXISTS (SELECT 1
               FROM sys.indexes
               WHERE object_id = OBJECT_ID('messaging.outbox')
                 AND name = 'idx_outbox_unfinished_key')
    CREATE INDEX "idx_outbox_unfinished_key" ON "messaging"."outbox" ("topic", "message_key", "id")
        WHERE "status" IN ('PENDING', 'PROCESSING');
GO
