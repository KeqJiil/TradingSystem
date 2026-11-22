using MediatR;
using Order.Domain;
using Order.Domain.Aggregates;

namespace Order.Application.UseCases.PlaceOrder;

public record PlaceOrderCommand(
    Guid UserId,
    Guid StockId,
    int Quantity,
    OrderSide Side,
    TimeInForce TimeInForce,
    OrderType Type,
    decimal? LimitPrice,
    Guid OrderId) : IRequest<Result<Guid>>;
