using Order.Domain.Aggregates;

namespace Order.Application.Abstractions;

public interface IOrderReader
{
    Task<OrderDetailsDto?> GetOrderByIdAsync(Guid orderId, Guid userId, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<OrderDto>> GetOrdersByUserIdAsync(Guid userId, OrderStatus? status = null, int? limit = 20, DateTimeOffset? afterCreatedAt = null, Guid? afterId = null, CancellationToken cancellationToken = default);
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
    int Quantity,
    int FilledQuantity,
    OrderStatus Status,
    DateTimeOffset CreatedAt);

public record struct FillDto(Guid ExecutionId, decimal Quantity, decimal Price, DateTimeOffset FilledAt);