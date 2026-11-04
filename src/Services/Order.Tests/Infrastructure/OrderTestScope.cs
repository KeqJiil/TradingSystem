using MediatR;
using Messaging.Abstractions;
using Messaging.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Order.Tests.Domain.Aggregates;
using OrderAggregate = Order.Domain.Aggregates.Order;

namespace Order.Tests.Infrastructure;

public sealed class OrderTestScope : IAsyncDisposable
{
    private readonly MssqlFixture _fixture;
    private readonly ServiceProvider _provider;

    private OrderTestScope(MssqlFixture fixture, Action<IServiceCollection>? configure)
    {
        _fixture = fixture;
        _provider = OrderServices.Build(fixture.ConnectionString, services =>
        {
            services.AddSingleton<TimeProvider>(Clock);
            configure?.Invoke(services);
        });
    }

    public static OrderTestScope Create(MssqlFixture fixture, Action<IServiceCollection>? configure = null) =>
        new(fixture, configure);

    public TestClock Clock { get; } = new(TestOrders.Now);

    public PublishedMessages Published => _provider.GetRequiredService<PublishedMessages>();

    public Task<TResponse> SendAsync<TResponse>(IRequest<TResponse> request) =>
        ExecuteAsync(sp => sp.GetRequiredService<ISender>().Send(request));

    public Task<ConsumeOutcome> ConsumeAsync<TConsumer, TMessage>(TMessage message)
        where TConsumer : IMessageConsumer<TMessage> =>
        ExecuteAsync(sp => sp.GetRequiredService<TConsumer>().ConsumeAsync(message, CancellationToken.None));

    public async Task<T> ExecuteAsync<T>(Func<IServiceProvider, Task<T>> action)
    {
        await using var scope = _provider.CreateAsyncScope();
        return await action(scope.ServiceProvider);
    }

    public async Task<OrderAggregate> SeedAsync(OrderAggregate order)
    {
        await using var context = _fixture.CreateContext();
        context.Orders.Add(order);
        await context.SaveChangesAsync();
        return order;
    }

    public async Task<OrderAggregate?> FindAsync(Guid orderId)
    {
        await using var context = _fixture.CreateContext();
        return await context.Orders.AsNoTracking().FirstOrDefaultAsync(o => o.Id == orderId);
    }

    public ValueTask DisposeAsync() => _provider.DisposeAsync();
}

public sealed class TestClock(DateTimeOffset now) : TimeProvider
{
    public DateTimeOffset Now { get; set; } = now;

    public override DateTimeOffset GetUtcNow() => Now;
}
