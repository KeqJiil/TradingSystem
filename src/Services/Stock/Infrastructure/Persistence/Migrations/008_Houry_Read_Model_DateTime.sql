DECLARE @defaultConstraintName NVARCHAR(200);

SELECT @defaultConstraintName = dc.name
FROM sys.default_constraints dc
         JOIN sys.columns c ON c.object_id = dc.parent_object_id AND c.column_id = dc.parent_column_id
WHERE dc.parent_object_id = OBJECT_ID('hourly_stock_data_projection')
  AND c.name = 'date';

IF @defaultConstraintName IS NOT NULL
    EXEC ('ALTER TABLE "hourly_stock_data_projection" DROP CONSTRAINT [' + @defaultConstraintName + ']');

DROP INDEX "idx_hourly_stock_projection_aggregate_id_date_hour" ON "hourly_stock_data_projection";

ALTER TABLE "hourly_stock_data_projection"
    DROP COLUMN "date";

ALTER TABLE "hourly_stock_data_projection"
    ADD "date_time" DATETIMEOFFSET NOT NULL DEFAULT (SYSDATETIMEOFFSET());

CREATE UNIQUE INDEX "idx_hourly_stock_projection_aggregate_id_date_time" ON "hourly_stock_data_projection" ("aggregate_id", "date_time");
