using MediatR;

namespace Stock.Application.Commands.CreateStock;

public record CreateStockCommand(
    string Name,
    string Currency,
    bool IsOpenToTrade,
    TimeOnly OpenTime,
    TimeOnly CloseTime) : IRequest<Guid>;