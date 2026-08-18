using MediatR;

namespace  Stock.Application.Commands.UpdateStock;

public record UpdateStockCommand(
    Guid AggregateId,
    decimal PriceChange) : IRequest<Guid>;