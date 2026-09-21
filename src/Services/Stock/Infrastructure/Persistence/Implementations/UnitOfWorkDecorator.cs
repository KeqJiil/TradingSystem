using Polly;
using Stock.Application.Abstractions;

namespace Stock.Infrastructure.Persistence.Implementations;

public class UnitOfWorkDecorator : IUnitOfWorkDecorator
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly ResiliencePipeline _pipeline;

    public UnitOfWorkDecorator(IUnitOfWork unitOfWork, ResiliencePipeline pipeline)
    {
        _unitOfWork = unitOfWork;
        _pipeline = pipeline;
    }

    public async Task ExecuteAsync(Func<Task> action, CancellationToken ct)
    {
        if (ResilienceScope.IsActive)
        {
            await RunInTransactionAsync(action, ct);
            return;
        }

        await _pipeline.ExecuteAsync(async (state, token) =>
        {
            ResilienceScope.IsActive = true;
            await state.Decorator.RunInTransactionAsync(state.Action, token);
        }, (Decorator: this, Action: action), ct);
    }

    private async Task RunInTransactionAsync(Func<Task> action, CancellationToken ct)
    {
        await _unitOfWork.StartTransactionAsync(ct);

        try
        {
            await action();
            await _unitOfWork.CommitAsync(ct);
        }
        catch
        {
            await _unitOfWork.RollbackAsync(ct);
            throw;
        }
    }
}
