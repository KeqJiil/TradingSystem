using MediatR;

namespace Stock.Application.Commands.CreateReadModel;

public record CreateReadModelCommand(
    Guid AggregateId, string Name, 
    bool IsOpenToTrade, string Currency,
    TimeOnly TradingStartTime,
    TimeOnly TradingCloseTime) : IRequest;