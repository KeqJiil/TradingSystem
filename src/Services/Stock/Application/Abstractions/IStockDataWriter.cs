namespace Stock.Application.Abstractions;

public interface IStockWriter
{
    public Task CreateAsync(Guid id, CreateStockDto dto, CancellationToken ct);
    public Task ChangeName(Guid id, string name, CancellationToken ct);
    public Task ChangeTime(Guid id, TimeOnly openTime, TimeOnly closeTime, CancellationToken ct);
    public Task ToggleOpenToTrade(Guid id, CancellationToken ct);
}

public record CreateStockDto(string Name, bool IsOpenToTrade, TimeOnly OpenTime, TimeOnly CloseTime, string Currency);