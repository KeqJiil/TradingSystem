namespace Stock.Application.Abstractions;

public interface IStockWriter
{
    public Task<bool> CreateAsync(Guid id, CreateStockDto dto, CancellationToken ct);
    public Task<long?> ChangeTime(Guid id, TimeOnly openTime, TimeOnly closeTime, CancellationToken ct);
    public Task<long?> ChangeName(Guid id, string name, CancellationToken ct);
    public Task<long?> SetOpenToTrade(Guid id, bool isOpenToTrade, CancellationToken ct);
}

public record CreateStockDto(string Name, bool IsOpenToTrade, TimeOnly OpenTime, TimeOnly CloseTime, string Currency);
