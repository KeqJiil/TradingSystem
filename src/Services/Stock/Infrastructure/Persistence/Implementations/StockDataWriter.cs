using Dapper;
using Microsoft.Data.SqlClient;
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
            SET name = @Name, metadata_version = metadata_version + 1
            OUTPUT inserted.metadata_version
            WHERE id = @Id
            """,
            new { Id = id, Name = name },
            dbContext.Transaction
        );
    }

    public async Task<bool> CreateAsync(Guid id, CreateStockDto dto, CancellationToken ct)
    {
        await dbContext.EnsureConnectionOpenAsync(ct);

        try
        {
            await dbContext.Connection.ExecuteAsync(
                "INSERT INTO stock_data (id, name, is_open_to_trade, trading_start_time, trading_end_time, currency) VALUES (@Id, @Name, @IsOpenToTrade, @OpenTime, @CloseTime, @Currency)",
                new { Id = id, dto.Name, dto.IsOpenToTrade, dto.OpenTime, dto.CloseTime, dto.Currency },
                dbContext.Transaction
            );

            return true;
        }
        catch (SqlException ex) when (ex.Number == 2627)
        {
            return false;
        }
    }

    public async Task<long?> ChangeTime(Guid id, TimeOnly openTime, TimeOnly closeTime, CancellationToken ct)
    {
        await dbContext.EnsureConnectionOpenAsync(ct);

        return await dbContext.Connection.QuerySingleOrDefaultAsync<long?>(
            """
            UPDATE stock_data
            SET trading_start_time = @OpenTime, trading_end_time = @CloseTime,
                metadata_version = metadata_version + 1
            OUTPUT inserted.metadata_version
            WHERE id = @Id
            """,
            new { Id = id, OpenTime = openTime, CloseTime = closeTime },
            dbContext.Transaction
        );
    }

    public async Task<long?> SetOpenToTrade(Guid id, bool isOpenToTrade, CancellationToken ct)
    {
        await dbContext.EnsureConnectionOpenAsync(ct);

        return await dbContext.Connection.QuerySingleOrDefaultAsync<long?>(
            """
            UPDATE stock_data
            SET is_open_to_trade = @IsOpenToTrade, metadata_version = metadata_version + 1
            OUTPUT inserted.metadata_version
            WHERE id = @Id
            """,
            new { Id = id, IsOpenToTrade = isOpenToTrade },
            dbContext.Transaction
        );
    }
}
