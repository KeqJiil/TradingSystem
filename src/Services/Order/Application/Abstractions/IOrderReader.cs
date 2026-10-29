using Order.Domain.Aggregates;

namespace Order.Application.Abstractions;

public interface IOrderReader
{
    Task<OrderDetailsDto?> GetOrderByIdAsync(Guid orderId, Guid userId, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<OrderDto>> GetOrdersByUserIdAsync(Guid userId, int limit, OrderStatus? status = null,
        DateTimeOffset? afterCreatedAt = null, Guid? afterId = null, CancellationToken cancellationToken = default);
}

public record OrderDetailsDto(
    OrderDto Order,
    TimeInForce TimeInForce,
    DateTimeOffset? ApprovedAt,
    DateTimeOffset? ClosedAt,
    string? CloseReason,
    IReadOnlyList<FillDto> Fills);

public record OrderDto(
    Guid Id,
    Symbols Symbol,
    OrderSide Side,
    OrderType Type,
    decimal? LimitPrice,
    decimal Quantity,
    decimal FilledQuantity,
    OrderStatus Status,
    DateTimeOffset CreatedAt);

public record struct FillDto(Guid ExecutionId, decimal Quantity, decimal Price, DateTimeOffset FilledAt);