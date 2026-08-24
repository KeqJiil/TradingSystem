using Dapper;
using Stock.Application.Abstractions;

namespace Stock.Infrastructure.Persistence.Implementations;

public class StockDataWriter(IDbContext dbContext) : IStockWriter
{
    
    public async Task ChangeName(Guid id, string name, CancellationToken ct)
    {
        await dbContext.EnsureConnectionOpenAsync(ct);
        
        await dbContext.Connection.ExecuteAsync(
            "UPDATE stock_data SET Name = @Name WHERE Id = @Id",
            new { Id = id, Name = name },
            dbContext.Transaction
        );
    }

    public async Task CreateAsync(Guid id, CreateStockDto dto, CancellationToken ct)
    {
        await dbContext.EnsureConnectionOpenAsync(ct);
        await dbContext.Connection.ExecuteAsync(
            "INSERT INTO stock_data (id, name, is_open_to_trade, trading_start_time, trading_end_time, currency) VALUES (@Id, @Name, @OpenTime, @CloseTime, @IsOpenToTrade, @Currency)",
            new { Id = id, dto.Name, dto.OpenTime, dto.CloseTime, dto.IsOpenToTrade, dto.Currency },
            dbContext.Transaction
        );
    }

    public async Task ChangeTime(Guid id, TimeOnly openTime, TimeOnly closeTime, CancellationToken ct)
    {
        await dbContext.EnsureConnectionOpenAsync(ct);
        
        await dbContext.Connection.ExecuteAsync(
            "UPDATE stock_data SET OpenTime = @OpenTime, CloseTime = @CloseTime WHERE Id = @Id",
            new { Id = id, OpenTime = openTime, CloseTime = closeTime },
            dbContext.Transaction
        );
    }

    public async Task ToggleOpenToTrade(Guid id, CancellationToken ct)
    {
        await dbContext.EnsureConnectionOpenAsync(ct);
        await dbContext.Connection.ExecuteAsync(
            "UPDATE stock_data SET IsOpenToTrade = NOT IsOpenToTrade WHERE Id = @Id",
            new { Id = id },
            dbContext.Transaction
        );
    }
}