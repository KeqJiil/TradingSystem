using System.Data.Common;

namespace Messaging.Dapper;

public interface ITransactionContext
{
    DbConnection Connection { get; }
    DbTransaction? Transaction { get; }

    Task EnsureConnectionOpenAsync(CancellationToken cancellationToken);
}