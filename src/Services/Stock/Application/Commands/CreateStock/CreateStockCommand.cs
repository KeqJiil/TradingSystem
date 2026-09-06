using MediatR;

namespace Stock.Application.Commands.CreateStock;

public record CreateStockCommand(
    string Name,
    bool IsOpenToTrade,
    string Currency,
    TimeOnly TradingStartTime,
    TimeOnly TradingEndTime) : IRequest<Guid>;
