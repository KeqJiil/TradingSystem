using MediatR;
using Messaging.Dapper.Transactions;
using Microsoft.Extensions.DependencyInjection;
using Polly;
using Polly.Retry;
using Stock.Infrastructure.MediatrPipelines;
using Xunit;

namespace Stock.Tests.Infrastructure.MediatrPipelines;

public class ResiliencePipelineBehaviourTests(MssqlFixture fixture) : IClassFixture<MssqlFixture>, IAsyncLifetime
{
    private const int MaxRetryAttempts = 3;

    private readonly ResiliencePipeline _pipeline = new ResiliencePipelineBuilder()
        .AddRetry(new RetryStrategyOptions
        {
            ShouldHandle = new PredicateBuilder().Handle<InvalidOperationException>(),
            MaxRetryAttempts = MaxRetryAttempts,
            Delay = TimeSpan.Zero
        })
        .Build();

    private ServiceProvider _provider = null!;

    public Task InitializeAsync()
    {
        _provider = StockServices.Build(fixture.ConnectionString);
        return Task.CompletedTask;
    }

    public async Task DisposeAsync()
    {
        await _provider.DisposeAsync();
    }

    [Fact]
    public async Task Handle_InsideBehaviour_MarksLibraryResilienceScopeActive()
    {
        var behaviour = new ResiliencePipelineBehaviour<Ping, Unit>(_pipeline);
        var active = false;

        await behaviour.Handle(new Ping(), ct =>
        {
            active = ResilienceScope.IsActive;
            return Task.FromResult(Unit.Value);
        }, CancellationToken.None);

        Assert.True(active);
        Assert.False(ResilienceScope.IsActive);
    }

    [Fact]
    public async Task UnitOfWork_InsideBehaviour_IsNotRetriedOnItsOwn_OnlyTheOuterLevelRetries()
    {
        var behaviour = new ResiliencePipelineBehaviour<Ping, Unit>(_pipeline);
        var calls = 0;

        await Assert.ThrowsAsync<InvalidOperationException>(() => behaviour.Handle(new Ping(), async ct =>
        {
            await using var scope = _provider.CreateAsyncScope();
            await scope.ServiceProvider.GetRequiredService<IUnitOfWorkDecorator>().ExecuteAsync(() =>
            {
                calls++;
                throw new InvalidOperationException("transient");
            }, ct);
            return Unit.Value;
        }, CancellationToken.None));

        Assert.Equal(MaxRetryAttempts + 1, calls);
    }

    [Fact]
    public async Task UnitOfWork_InsideBehaviour_SucceedsAfterTransientFailure()
    {
        var behaviour = new ResiliencePipelineBehaviour<Ping, Unit>(_pipeline);
        var calls = 0;

        await behaviour.Handle(new Ping(), async ct =>
        {
            await using var scope = _provider.CreateAsyncScope();
            await scope.ServiceProvider.GetRequiredService<IUnitOfWorkDecorator>().ExecuteAsync(() =>
            {
                if (++calls == 1) throw new InvalidOperationException("transient");
                return Task.CompletedTask;
            }, ct);
            return Unit.Value;
        }, CancellationToken.None);

        Assert.Equal(2, calls);
    }

    private sealed record Ping : IRequest<Unit>;
}
