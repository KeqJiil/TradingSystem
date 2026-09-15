using Dapper;

namespace Stock.Tests.Infrastructure;

public static class StockTestSchema
{
    public static async Task EnsureStockDataCreatedAsync(TestDbContext dbContext)
    {
        await dbContext.Connection.ExecuteAsync("""
            IF OBJECT_ID('stock_data') IS NULL
            CREATE TABLE stock_data (
                id UNIQUEIDENTIFIER PRIMARY KEY,
                name NVARCHAR(100) NOT NULL,
                is_open_to_trade BIT NOT NULL,
                trading_start_time TIME NOT NULL,
                trading_end_time TIME NOT NULL,
                currency NVARCHAR(10) NOT NULL,
                created_at DATETIMEOFFSET NOT NULL DEFAULT(GETDATE())
            )
            """);
    }

    public static async Task EnsureDailyStockDataProjectionCreatedAsync(TestDbContext dbContext)
    {
        await dbContext.Connection.ExecuteAsync("""
            IF OBJECT_ID('daily_stock_data_projection') IS NULL
            CREATE TABLE daily_stock_data_projection (
                id UNIQUEIDENTIFIER PRIMARY KEY,
                aggregate_id UNIQUEIDENTIFIER NOT NULL,
                open_price DECIMAL(20, 4) NOT NULL,
                low_price DECIMAL(20, 4) NOT NULL,
                high_price DECIMAL(20, 4) NOT NULL,
                close_price DECIMAL(20, 4) NOT NULL,
                price_difference DECIMAL(20, 4) NOT NULL,
                last_version BIGINT NOT NULL,
                date DATE NOT NULL DEFAULT(GETDATE())
            )
            """);
    }

    public static async Task EnsureEventsStoreCreatedAsync(TestDbContext dbContext)
    {
        await dbContext.Connection.ExecuteAsync("""
            IF OBJECT_ID('events_store') IS NULL
            CREATE TABLE events_store (
                event_id UNIQUEIDENTIFIER NOT NULL PRIMARY KEY,
                aggregate_id UNIQUEIDENTIFIER NOT NULL,
                version BIGINT NOT NULL,
                event_type NVARCHAR(50) NOT NULL,
                payload NVARCHAR(MAX) NOT NULL,
                price_change DECIMAL(20, 4) NOT NULL,
                created_at DATETIMEOFFSET NOT NULL DEFAULT(GETDATE()),
                occured_at DATETIMEOFFSET NOT NULL DEFAULT(GETDATE())
            )
            """);
    }

    public static async Task EnsureStockDataProjectionCreatedAsync(TestDbContext dbContext)
    {
        await dbContext.Connection.ExecuteAsync("""
            IF OBJECT_ID('stock_data_projection') IS NULL
            CREATE TABLE stock_data_projection (
                aggregate_id UNIQUEIDENTIFIER PRIMARY KEY,
                name NVARCHAR(100) NOT NULL,
                is_open_to_trade BIT NOT NULL,
                price Decimal(20, 4) NOT NULL,
                version BIGINT NOT NULL,
                trading_start_time TIME NOT NULL,
                trading_end_time TIME NOT NULL,
                currency NVARCHAR(10) NOT NULL,
                updated_at DATETIME2 NOT NULL DEFAULT GETUTCDATE(),
                created_at DATETIME2 NOT NULL DEFAULT GETUTCDATE()
            )
            """);
    }
}
