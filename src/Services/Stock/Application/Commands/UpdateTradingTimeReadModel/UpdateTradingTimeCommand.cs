using MediatR;

namespace Stock.Application.Commands.UpdateTradingTimeReadModel;

public record UpdateTradingTimeCommand(
    Guid AggregateId,
    TimeOnly TradingStartTime,
    TimeOnly TradingCloseTime,
    long Version) : IRequest;
