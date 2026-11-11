using System.Diagnostics;
using Messaging.Abstractions;
using Order.Application.UseCases.PlaceOrder;
using Order.Domain.Aggregates;
using TradingSystem.Contracts.ProtobufClasses.Order;
using Order.Infrastructure.Observability;
using Order.Tests.Infrastructure;
using Xunit;
using OrderSide = Order.Domain.Aggregates.OrderSide;
using OrderType = Order.Domain.Aggregates.OrderType;
using TimeInForce = Order.Domain.Aggregates.TimeInForce;

namespace Order.Tests.Infrastructure.Observability;

public class TracingPropagationTests(MssqlFixture fixture) : IClassFixture<MssqlFixture>, IAsyncDisposable
{
    private const string AmbientSource = "order-tests-ambient";

    private readonly OrderTestScope _scope = OrderTestScope.Create(fixture);

    public ValueTask DisposeAsync() => _scope.DisposeAsync();

    [Fact]
    public async Task PlaceOrder_InsideTrace_WritesTraceParentAndCorrelationToPublishedEvent()
    {
        using var listener = new ActivityListener
        {
            ShouldListenTo = source => source.Name is AmbientSource or OrderTelemetry.SourceName
                or global::Messaging.MessagingTelemetry.SourceName,
            Sample = (ref _) => ActivitySamplingResult.AllDataAndRecorded
        };
        ActivitySource.AddActivityListener(listener);

        using var ambientSource = new ActivitySource(AmbientSource);
        using var ambient = ambientSource.StartActivity("ambient")!;
        MessagingCorrelation.CorrelationId = "corr-1";

        var result = await _scope.SendAsync(new PlaceOrderCommand(
            UserId: Guid.NewGuid(),
            Symbol: Symbols.Test,
            Quantity: 1,
            Side: OrderSide.Buy,
            TimeInForce: TimeInForce.Gtc,
            Type: OrderType.Limit,
            LimitPrice: 100m));

        Assert.True(result.IsSuccess);
        var published = Assert.Single(await _scope.Published.OfTypeAsync<OrderPlacedEvent>(result.Value.ToString()));
        Assert.StartsWith($"00-{ambient.TraceId.ToHexString()}-", published.Headers[MessagingHeaders.TraceParent]);
        Assert.Equal("corr-1", published.Headers[MessagingHeaders.CorrelationId]);
    }
}
