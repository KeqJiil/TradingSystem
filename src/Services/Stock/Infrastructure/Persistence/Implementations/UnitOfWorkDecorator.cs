using Stock.Application.Abstractions;

namespace Stock.Infrastructure.Persistence.Implementations;

public class UnitOfWorkDecorator : IUnitOfWorkDecorator
{
    private readonly IUnitOfWork _unitOfWork;
    
    public UnitOfWorkDecorator(IUnitOfWork unitOfWork)
    {
        _unitOfWork = unitOfWork;
    }
    
    public async Task ExecuteAsync(Func<Task> action, CancellationToken ct)
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