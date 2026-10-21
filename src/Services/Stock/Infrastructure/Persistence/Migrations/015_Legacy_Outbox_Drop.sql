IF OBJECT_ID('dbo.outbox', 'U') IS NOT NULL
    DROP TABLE dbo.outbox;

IF EXISTS(SELECT 1
            FROM sys.types
            WHERE is_table_type = 1
            AND name = 'OutboxEventTvp')
    DROP TYPE dbo.OutboxEventTvp;
