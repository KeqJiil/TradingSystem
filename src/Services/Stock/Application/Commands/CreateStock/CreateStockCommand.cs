using MediatR;

namespace Stock.Application.Commands.CreateStock;

public record CreateStockCommand(
    Guid Id,
    string Name,
    bool IsOpenToTrade,
    string Currency,
    TimeOnly TradingStartTime,
    TimeOnly TradingEndTime) : IRequest<CreateStockResult>;

public enum CreateStockResult
{
    Created,
    AlreadyExists,
    Conflict
}
