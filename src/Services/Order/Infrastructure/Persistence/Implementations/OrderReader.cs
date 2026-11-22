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
            .LeftJoin(dbContext.Stocks, o => o.StockId, s => s.Id,
                (o, s) => new { o, StockName = s == null ? null : s.Name })
            .Select(x => MapToOrderDetailsDto(x.o, x.StockName))
            .AsNoTracking()
            .FirstOrDefaultAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<OrderDto>> GetOrdersByUserIdAsync(Guid userId, int limit,
        OrderStatus? status = null,
        DateTimeOffset? afterCreatedAt = null,
        Guid? afterId = null, CancellationToken cancellationToken = default)
    {
        var query = dbContext.Orders
            .Where(x => x.UserId == userId)
            .AsNoTracking();

        if (status.HasValue) query = query.Where(x => x.Status == status.Value);

        if (afterCreatedAt.HasValue && afterId.HasValue)
            query = query.Where(x => x.CreatedAt < afterCreatedAt.Value ||
                                     (x.CreatedAt == afterCreatedAt.Value && x.Id < afterId.Value));

        return await query
            .LeftJoin(dbContext.Stocks, o => o.StockId, s => s.Id,
                (o, s) => new { o, StockName = s == null ? null : s.Name })
            .OrderByDescending(x => x.o.CreatedAt)
            .ThenByDescending(x => x.o.Id)
            .Take(limit)
            .Select(x => MapToOrderDto(x.o, x.StockName))
            .ToListAsync(cancellationToken);
    }

    private static OrderDto MapToOrderDto(AggregateOrder x, string? stockName)
    {
        return new OrderDto(
            x.Id,
            x.StockId,
            stockName,
            x.OrderSide,
            x.OrderTerms.Type,
            x.OrderTerms.LimitPrice?.Value,
            x.Quantity.Value,
            x.FilledQuantity,
            x.Status,
            x.CreatedAt
        );
    }

    private static OrderDetailsDto MapToOrderDetailsDto(AggregateOrder x, string? stockName)
    {
        return new OrderDetailsDto(
            MapToOrderDto(x, stockName),
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