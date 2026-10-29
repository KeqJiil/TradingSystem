using Microsoft.EntityFrameworkCore;
using AggregateOrder = Order.Domain.Aggregates.Order;
using Order.Application.Abstractions;
using Order.Domain.Aggregates;
using Order.Infrastructure.Persistence.Configuration;

namespace Order.Infrastructure.Persistence.Implementations;

public class OrderReader(OrderDbContext dbContext) : IOrderReader
{
    public Task<OrderDetailsDto?> GetOrderByIdAsync(Guid orderId, Guid userId,
        CancellationToken cancellationToken = default)
    {
        return dbContext.Orders
            .Where(x => x.Id == orderId && x.UserId == userId)
            .Select(x => MapToOrderDetailsDto(x))
            .FirstOrDefaultAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<OrderDto>> GetOrdersByUserIdAsync(Guid userId, int limit,
        OrderStatus? status = null,
        DateTimeOffset? afterCreatedAt = null,
        Guid? afterId = null, CancellationToken cancellationToken = default)
    {
        var query = dbContext.Orders
            .Where(x => x.UserId == userId);

        if (status.HasValue) query = query.Where(x => x.Status == status.Value);

        if (afterCreatedAt.HasValue && afterId.HasValue)
            query = query.Where(x => x.CreatedAt < afterCreatedAt.Value ||
                                     (x.CreatedAt == afterCreatedAt.Value && x.Id < afterId.Value));

        return await query
            .OrderByDescending(x => x.CreatedAt)
            .ThenByDescending(x => x.Id)
            .Take(limit)
            .Select(x => MapToOrderDto(x))
            .ToListAsync(cancellationToken);
    }

    private static OrderDto MapToOrderDto(AggregateOrder x)
    {
        return new OrderDto(
            x.Id,
            x.Symbol,
            x.OrderSide,
            x.OrderTerms.Type,
            x.OrderTerms.LimitPrice?.Value,
            x.Quantity.Value,
            x.FilledQuantity,
            x.Status,
            x.CreatedAt
        );
    }

    private static OrderDetailsDto MapToOrderDetailsDto(AggregateOrder x)
    {
        return new OrderDetailsDto(
            MapToOrderDto(x),
            x.OrderTerms.TimeInForce,
            x.ApprovedAt,
            x.CanceledAt,
            x.CancellationReason,
            x.Fills.Select(f => new FillDto(
                f.ExecutionId,
                f.Quantity.Value,
                f.Price.Value,
                f.FilledAt)
            ).ToList().AsReadOnly()
        );
    }
}