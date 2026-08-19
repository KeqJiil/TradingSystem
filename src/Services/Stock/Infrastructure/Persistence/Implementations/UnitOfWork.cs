using System.Data.Common;
using Stock.Application.Abstractions;

namespace Stock.Infrastructure.Persistence.Implementations;

public class UnitOfWork : IUnitOfWork
{
    private readonly IDbConnectionFactory _connectionFactory;
    private DbConnection? _connection;
    private DbTransaction? _transaction;
    
    public UnitOfWork(IDbConnectionFactory connectionFactory)
    {
        _connectionFactory = connectionFactory;
    }
    
    public async Task StartTransactionAsync(CancellationToken ct)
    {
        _connection = _connectionFactory.CreateConnection();
        await _connection.OpenAsync(ct);
        _transaction = await _connection.BeginTransactionAsync(ct);
    }
    
    public async ValueTask CommitAsync(CancellationToken ct)
    {
        if (_transaction is not null)
        {
            try
            {
                await _transaction.CommitAsync(ct);
            }
            finally
            {
                await DisposeAsync();
            }
        }
    }

    public async ValueTask RollbackAsync(CancellationToken ct)
    {
        if (_transaction is not null)
        {
            try
            {
                await _transaction.RollbackAsync(ct);
            }
            finally
            {
                await DisposeAsync();
            }
        }
    }

    public async ValueTask DisposeAsync()
    {
        if (_connection != null)
        {
            await _connection.CloseAsync();
            await _connection.DisposeAsync();
        }

        if (_transaction != null)
        {
            await _transaction.DisposeAsync();
        }
    }
}