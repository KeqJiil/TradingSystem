using System.Data;
using System.Data.Common;
using Microsoft.Data.SqlClient;
using Stock.Infrastructure.Persistence;

namespace Stock.Tests.Infrastructure;

public sealed class TestDbContext : IDbContext, IAsyncDisposable
{
    private readonly SqlConnection _connection;

    public TestDbContext(string connectionString)
    {
        _connection = new SqlConnection(connectionString);
    }

    public DbConnection Connection => _connection;
    public DbTransaction? Transaction => null;

    public async Task EnsureConnectionOpenAsync(CancellationToken ct)
    {
        if (_connection.State != ConnectionState.Open)
            await _connection.OpenAsync(ct);
    }

    public async ValueTask DisposeAsync() => await _connection.DisposeAsync();
}
