using MediatR;

namespace Stock.Application.Commands.ChangePrice;

public record ChangePriceCommand(Guid EventId, Guid AggregateId, decimal PriceChange, DateTimeOffset OccuredAt) : IRequest;
