using System.Data;
using System.Data.Common;
using Stock.Application.Abstractions;

namespace Stock.Infrastructure.Persistence.Implementations;

public class UnitOfWork : IUnitOfWork, IDbContext
{
    private readonly IDbConnectionFactory _connectionFactory;
    public DbConnection Connection { get; private set; }
    public DbTransaction? Transaction { get; private set; }

    public UnitOfWork(IDbConnectionFactory connectionFactory)
    {
        _connectionFactory = connectionFactory;
        Connection = _connectionFactory.CreateConnection();
    }
    
    public async Task EnsureConnectionOpenAsync(CancellationToken ct)
    {
        if (Connection.State == ConnectionState.Closed)
        {
            await Connection.OpenAsync(ct);
        }
    }

    public async Task StartTransactionAsync(CancellationToken ct)
    {
        await EnsureConnectionOpenAsync(ct);
        Transaction = await Connection.BeginTransactionAsync(ct);
    }
    
    public async ValueTask CommitAsync(CancellationToken ct)
    {
        if (Transaction is not null)
        {
            try
            {
                await Transaction.CommitAsync(ct);
            }
            finally
            {
                await DisposeAsync();
            }
        }
    }

    public async ValueTask RollbackAsync(CancellationToken ct)
    {
        if (Transaction is not null)
        {
            try
            {
                await Transaction.RollbackAsync(ct);
            }
            finally
            {
                await DisposeAsync();
            }
        }
    }

    public async ValueTask DisposeAsync()
    {
        await Connection.CloseAsync();
        await Connection.DisposeAsync();

        if (Transaction != null)
        {
            await Transaction.DisposeAsync();
        }
    }
}