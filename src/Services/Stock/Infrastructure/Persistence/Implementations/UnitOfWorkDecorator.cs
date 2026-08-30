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
        await _pipeline.ExecuteAsync(async (state, token) =>
        {
            await _unitOfWork.StartTransactionAsync(token);

            try
            {
                await state.Action();
                await state.UnitOfWork.CommitAsync(token);
            }
            catch
            {
                await state.UnitOfWork.RollbackAsync(token);
                throw;
            }
        }, (UnitOfWork: _unitOfWork, Action: action), ct);
    }
}