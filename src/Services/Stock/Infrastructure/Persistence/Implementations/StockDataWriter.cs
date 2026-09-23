using Dapper;
using Stock.Application.Abstractions;

namespace Stock.Infrastructure.Persistence.Implementations;

public class StockDataWriter(IDbContext dbContext) : IStockWriter
{
    public async Task<long?> ChangeName(Guid id, string name, CancellationToken ct)
    {
        await dbContext.EnsureConnectionOpenAsync(ct);

        return await dbContext.Connection.QuerySingleOrDefaultAsync<long?>(
            """
            UPDATE stock_data
            SET name = @Name, status_version = status_version + 1
            OUTPUT inserted.status_version
            WHERE id = @Id
            """,
            new { Id = id, Name = name },
            dbContext.Transaction
        );
    }

    public async Task CreateAsync(Guid id, CreateStockDto dto, CancellationToken ct)
    {
        await dbContext.EnsureConnectionOpenAsync(ct);
        await dbContext.Connection.ExecuteAsync(
            "INSERT INTO stock_data (id, name, is_open_to_trade, trading_start_time, trading_end_time, currency) VALUES (@Id, @Name, @IsOpenToTrade, @OpenTime, @CloseTime, @Currency)",
            new { Id = id, dto.Name, dto.IsOpenToTrade, dto.OpenTime, dto.CloseTime, dto.Currency },
            dbContext.Transaction
        );
    }

    public async Task<long?> ChangeTime(Guid id, TimeOnly openTime, TimeOnly closeTime, CancellationToken ct)
    {
        await dbContext.EnsureConnectionOpenAsync(ct);

        return await dbContext.Connection.QuerySingleOrDefaultAsync<long?>(
            """
            UPDATE stock_data
            SET trading_start_time = @OpenTime, trading_end_time = @CloseTime,
                status_version = status_version + 1
            OUTPUT inserted.status_version
            WHERE id = @Id
            """,
            new { Id = id, OpenTime = openTime, CloseTime = closeTime },
            dbContext.Transaction
        );
    }

    public async Task<StockStatusChange?> ToggleOpenToTrade(Guid id, CancellationToken ct)
    {
        await dbContext.EnsureConnectionOpenAsync(ct);
        return await dbContext.Connection.QuerySingleOrDefaultAsync<StockStatusChange>(
            """
            UPDATE stock_data
            SET is_open_to_trade = 1 - is_open_to_trade, status_version = status_version + 1
            OUTPUT inserted.is_open_to_trade AS IsOpenToTrade, inserted.status_version AS StatusVersion
            WHERE Id = @Id
            """,
            new { Id = id },
            dbContext.Transaction
        );
    }
}