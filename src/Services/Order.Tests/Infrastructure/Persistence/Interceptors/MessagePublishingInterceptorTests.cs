using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using TradingSystem.Contracts.ProtobufClasses.Order;
using Order.Infrastructure.Persistence.Configuration;
using Order.Infrastructure.Topics;
using Order.Tests.Domain.Aggregates;
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
        Assert.Equal(order.Id.ToString(), published.Message.AggregateId);
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
    public async Task SaveChanges_LoadedAggregateChanged_PublishesItsEvent()
    {
        var order = await _scope.SeedAsync(TestOrders.PendingNew());

        await _scope.ExecuteAsync(async sp =>
        {
            var context = sp.GetRequiredService<OrderDbContext>();
            var tracked = await context.Orders.SingleAsync(o => o.Id == order.Id);
            tracked.Approve(TestOrders.Now);
            return await context.SaveChangesAsync();
        });

        Assert.Single(await _scope.Published.OfTypeAsync<OrderApprovedEvent>(order.Id.ToString()));
        Assert.Empty(await _scope.Published.OfTypeAsync<OrderPlacedEvent>(order.Id.ToString()));
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
}
