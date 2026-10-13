using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Polly;

namespace Messaging.Dapper.Transactions;

internal sealed class UnitOfWorkDecorator(
    IUnitOfWork unitOfWork,
    ITransactionContext transactionContext,
    [FromKeyedServices(UnitOfWorkDecorator.PipelineKey)] ResiliencePipeline pipeline,
    ILogger<UnitOfWorkDecorator> logger) : IUnitOfWorkDecorator
{
    public const string PipelineKey = "Messaging.Dapper.SqlTransientRetry";

    public async Task ExecuteAsync(Func<Task> action, CancellationToken ct)
    {
        if (ResilienceScope.IsActive || transactionContext.Transaction is not null)
        {
            await RunInTransactionAsync(action, ct);
            return;
        }

        await pipeline.ExecuteAsync(static async (state, token) =>
        {
            ResilienceScope.IsActive = true;
            await state.Decorator.RunInTransactionAsync(state.Action, token);
        }, (Decorator: this, Action: action), ct);
    }

    private async Task RunInTransactionAsync(Func<Task> action, CancellationToken ct)
    {
        if (!await unitOfWork.StartTransactionAsync(ct))
        {
            await action();
            return;
        }

        try
        {
            await action();
            await unitOfWork.CommitAsync(ct);
        }
        catch
        {
            try
            {
                await unitOfWork.RollbackAsync(CancellationToken.None);
            }
            catch (Exception rollbackException)
            {
                logger.LogError(rollbackException, "Rollback failed");
            }

            throw;
        }
    }
}
