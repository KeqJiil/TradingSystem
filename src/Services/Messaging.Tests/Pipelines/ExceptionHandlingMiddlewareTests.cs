using Messaging.Middlewares;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace Messaging.Tests.Pipelines;

public class ExceptionHandlingMiddlewareTests
{
    private static ServiceProvider BuildProvider(Action<IServiceCollection>? configure = null)
    {
        var services = new ServiceCollection();
        services.AddSingleton(typeof(ILogger<>), typeof(NullLogger<>));
        services.AddSingleton<IExceptionToOutcome, BasicExceptionToOutcome>();
        configure?.Invoke(services);
        new MessagingBuilder(services).AddDeliveryMiddleware<DefaultExceptionHandlingMiddleware>();
        return services.BuildServiceProvider();
    }

    [Fact]
    public async Task OnDelivery_ConsumerSucceeds_OutcomePassesThroughUnchanged()
    {
        await using var provider = BuildProvider();
        await using var scope = provider.CreateAsyncScope();
        var expected = new ConsumeOutcome(MessageConsumeResult.Success);

        var outcome = await MessagingPipeline.RunDelivery(DeliveryContexts.Sample(), scope.ServiceProvider,
            () => Task.FromResult(expected), CancellationToken.None);

        Assert.Equal(expected, outcome);
    }

    [Fact]
    public async Task OnDelivery_ConsumerThrows_ReturnsRetryWithExceptionMessage()
    {
        await using var provider = BuildProvider();
        await using var scope = provider.CreateAsyncScope();

        var outcome = await MessagingPipeline.RunDelivery(DeliveryContexts.Sample(), scope.ServiceProvider,
            () => throw new InvalidOperationException("boom"), CancellationToken.None);

        Assert.Equal(MessageConsumeResult.Retry, outcome.Kind);
        Assert.Equal("boom", outcome.Reason);
    }

    [Fact]
    public async Task OnDelivery_CancellationRequested_ExceptionPropagates()
    {
        await using var provider = BuildProvider();
        await using var scope = provider.CreateAsyncScope();
        using var cts = new CancellationTokenSource();
        await cts.CancelAsync();

        await Assert.ThrowsAsync<OperationCanceledException>(() => MessagingPipeline.RunDelivery(
            DeliveryContexts.Sample(), scope.ServiceProvider,
            () => throw new OperationCanceledException(cts.Token), cts.Token));
    }

    [Fact]
    public async Task OnDelivery_CustomMapperRegistered_MapperDecidesOutcome()
    {
        await using var provider = BuildProvider(services =>
            services.AddSingleton<IExceptionToOutcome, ExceptionTypeMapper>());
        await using var scope = provider.CreateAsyncScope();

        var outcome = await MessagingPipeline.RunDelivery(DeliveryContexts.Sample(), scope.ServiceProvider,
            () => throw new ArgumentException("bad data"), CancellationToken.None);

        Assert.Equal(MessageConsumeResult.DeadLetter, outcome.Kind);
        Assert.Equal("invalid", outcome.Reason);
    }
}
