IF EXISTS (SELECT 1
           FROM sys.columns
           WHERE object_id = OBJECT_ID('dbo.stock_data')
             AND name = 'status_version')
    EXEC sp_rename 'dbo.stock_data.status_version', 'metadata_version', 'COLUMN';
