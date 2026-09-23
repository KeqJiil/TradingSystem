ALTER TABLE dbo.events_store
    ADD position BIGINT IDENTITY (1, 1) NOT NULL;
GO

DECLARE @pk SYSNAME;
SELECT @pk = name
FROM sys.key_constraints
WHERE parent_object_id = OBJECT_ID('dbo.events_store')
  AND type = 'PK';
EXEC ('ALTER TABLE dbo.events_store DROP CONSTRAINT [' + @pk + ']');
GO

CREATE UNIQUE CLUSTERED INDEX idx_event_store_position ON dbo.events_store (position);
GO

ALTER TABLE dbo.events_store
    ADD CONSTRAINT pk_events_store PRIMARY KEY NONCLUSTERED (event_id);

CREATE UNIQUE NONCLUSTERED INDEX idx_event_store_version
    ON dbo.events_store (aggregate_id, version)
    INCLUDE (price_change, occured_at)
    WITH (DROP_EXISTING = ON);

DROP INDEX idx_event_store_aggregate_id ON dbo.events_store;

CREATE NONCLUSTERED INDEX idx_event_store_aggregate_occured_at
    ON dbo.events_store (aggregate_id, occured_at)
    INCLUDE (price_change, version);