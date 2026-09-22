using MediatR;
using Polly;
using Polly.Retry;
using Stock.Infrastructure.MediatrPipelines;
using Stock.Infrastructure.Persistence.Implementations;
using Xunit;

namespace Stock.Tests.Infrastructure.MediatrPipelines;

public class ResiliencePipelineBehaviourTests : IClassFixture<MssqlFixture>, IAsyncLifetime
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

    private readonly UnitOfWork _unitOfWork;
    private readonly UnitOfWorkDecorator _decorator;

    public ResiliencePipelineBehaviourTests(MssqlFixture fixture)
    {
        _unitOfWork = new UnitOfWork(new TestDbConnectionFactory(fixture.ConnectionString));
        _decorator = new UnitOfWorkDecorator(_unitOfWork, _pipeline);
    }

    public Task InitializeAsync()
    {
        return Task.CompletedTask;
    }

    public async Task DisposeAsync()
    {
        await _unitOfWork.DisposeAsync();
    }

    [Fact]
    public async Task UnitOfWork_OnItsOwn_RetriesTheTransaction()
    {
        var calls = 0;

        await Assert.ThrowsAsync<InvalidOperationException>(() => _decorator.ExecuteAsync(() =>
        {
            calls++;
            throw new InvalidOperationException("transient");
        }, CancellationToken.None));

        Assert.Equal(MaxRetryAttempts + 1, calls);
    }

    [Fact]
    public async Task UnitOfWork_InsideBehaviour_IsNotRetriedOnItsOwn_OnlyTheOuterLevelRetries()
    {
        var behaviour = new ResiliencePipelineBehaviour<Ping, Unit>(_pipeline);
        var calls = 0;

        await Assert.ThrowsAsync<InvalidOperationException>(() => behaviour.Handle(new Ping(), async ct =>
        {
            await _decorator.ExecuteAsync(() =>
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
            await _decorator.ExecuteAsync(() =>
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
