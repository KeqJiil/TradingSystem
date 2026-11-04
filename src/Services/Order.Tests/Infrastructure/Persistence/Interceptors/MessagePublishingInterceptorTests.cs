using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Order.Infrastructure.Messaging.Events;
using Order.Infrastructure.Persistence.Configuration;
using Order.Infrastructure.Topics;
using Order.Tests.Domain.Aggregates;
using Order.Tests.Infrastructure;
using OrderAggregate = Order.Domain.Aggregates.Order;
using Xunit;

namespace Order.Tests.Infrastructure.Persistence.Interceptors;

public class MessagePublishingInterceptorTests(MssqlFixture fixture) : IClassFixture<MssqlFixture>, IAsyncDisposable
{
    private readonly OrderTestScope _scope = OrderTestScope.Create(fixture);

    public ValueTask DisposeAsync() => _scope.DisposeAsync();

    [Fact]
    public async Task SaveChanges_AggregateWithTranslatableEvent_PublishesItKeyedByOrderId()
    {
        var order = TestOrders.PendingNew();

        await _scope.ExecuteAsync(async sp =>
        {
            var context = sp.GetRequiredService<OrderDbContext>();
            context.Orders.Add(order);
            return await context.SaveChangesAsync();
        });

        var published = Assert.Single(await _scope.Published.OfTypeAsync<OrderPlacedEvent>(order.Id.ToString()));
        Assert.Equal(order.Id, published.Message.AggregateId);
        Assert.Equal(OrderTopic.OrderEvents.Name(), published.Topic);
    }

    [Fact]
    public async Task SaveChanges_SeveralEventsInOneCommit_PublishesAll()
    {
        var order = TestOrders.PendingNew();
        order.RequestCancel(TestOrders.Now);

        await _scope.ExecuteAsync(async sp =>
        {
            var context = sp.GetRequiredService<OrderDbContext>();
            context.Orders.Add(order);
            return await context.SaveChangesAsync();
        });

        Assert.Single(await _scope.Published.OfTypeAsync<OrderPlacedEvent>(order.Id.ToString()));
        Assert.Single(await _scope.Published.OfTypeAsync<OrderCancelRequestedEvent>(order.Id.ToString()));
    }

    [Fact]
    public async Task SaveChanges_OnlyInternalEvents_PublishesNothing()
    {
        var order = await _scope.SeedAsync(TestOrders.PendingNew());
        order.ClearDomainEvents();

        await _scope.ExecuteAsync(async sp =>
        {
            var context = sp.GetRequiredService<OrderDbContext>();
            var tracked = await context.Orders.SingleAsync(o => o.Id == order.Id);
            tracked.Approve(TestOrders.Now);
            return await context.SaveChangesAsync();
        });

        Assert.Equal(0, await PublishedCountAsync(order.Id));
        var updated = await _scope.FindAsync(order.Id);
        Assert.NotNull(updated!.ApprovedAt);
    }

    [Fact]
    public async Task SaveChanges_CalledTwice_DoesNotRepublishEvents()
    {
        var order = TestOrders.PendingNew();

        await _scope.ExecuteAsync(async sp =>
        {
            var context = sp.GetRequiredService<OrderDbContext>();
            context.Orders.Add(order);
            await context.SaveChangesAsync();
            return await context.SaveChangesAsync();
        });

        Assert.Single(await _scope.Published.OfTypeAsync<OrderPlacedEvent>(order.Id.ToString()));
        Assert.Empty(order.GetDomainEvents());
    }

    [Fact]
    public async Task SaveChanges_TransactionRolledBack_PublishesNothing()
    {
        var order = TestOrders.PendingNew();

        await _scope.ExecuteAsync(async sp =>
        {
            var context = sp.GetRequiredService<OrderDbContext>();
            var strategy = context.Database.CreateExecutionStrategy();
            return await strategy.ExecuteAsync(async () =>
            {
                await using var transaction = await context.Database.BeginTransactionAsync();
                context.Orders.Add(order);
                var saved = await context.SaveChangesAsync();
                await transaction.RollbackAsync();
                return saved;
            });
        });

        Assert.Empty(await _scope.Published.OfTypeAsync<OrderPlacedEvent>(order.Id.ToString()));
        Assert.Null(await _scope.FindAsync(order.Id));
    }

    private async Task<int> PublishedCountAsync(Guid orderId)
    {
        var key = orderId.ToString();
        return (await _scope.Published.OfTypeAsync<OrderPlacedEvent>(key)).Count
               + (await _scope.Published.OfTypeAsync<OrderCancelRequestedEvent>(key)).Count
               + (await _scope.Published.OfTypeAsync<OrderCanceledEvent>(key)).Count
               + (await _scope.Published.OfTypeAsync<OrderFilledEvent>(key)).Count
               + (await _scope.Published.OfTypeAsync<OrderExpiredEvent>(key)).Count
               + (await _scope.Published.OfTypeAsync<OrderRejectedEvent>(key)).Count;
    }
}
