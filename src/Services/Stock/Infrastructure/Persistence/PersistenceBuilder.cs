using Stock.Application.Abstractions;
using Stock.Infrastructure.Persistence.Implementations;

namespace Stock.Infrastructure.Persistence;

public static class PersistenceBuilder
{
    public static void AddPersistence(this WebApplicationBuilder builder)
    {
        DapperTypeHandlers.Register();

        builder.Services.AddSingleton<IDbConnectionFactory, DbConnectionFactory>();

        builder.Services.AddScoped<IUnitOfWork, UnitOfWork>();
        builder.Services.AddScoped<IDbContext>(sp => (IDbContext)sp.GetRequiredService<IUnitOfWork>());
        builder.Services.AddScoped<IUnitOfWorkDecorator, UnitOfWorkDecorator>();

        builder.Services.AddScoped<IOutboxReader, OutboxReader>();
        builder.Services.AddScoped<IOutboxWriter, OutboxWriter>();
        builder.Services.AddScoped<IOutboxMarker, OutboxWriter>();

        builder.Services.AddScoped<IStockReader, StockReader>();
        builder.Services.AddScoped<IStockWriter, StockDataWriter>();
        builder.Services.AddScoped<IStockDataReader, StockDataReader>();

        builder.Services.AddScoped<IStockReadModelWriter, StockReadModelWriter>();
        builder.Services.AddScoped<IStockDailyReadModelWriter, StockDailyReadModelWriter>();
        builder.Services.AddScoped<IStockPriceHistoryReader, StockPriceHistoryReader>();

        builder.Services.AddScoped<IStockEventStore, StockEventStore>();
        builder.Services.AddScoped<IStockEventStoreReader, StockEventStoreReader>();
    }
}
