using System.Data.Common;

namespace Stock.Infrastructure.Persistence;

public interface IDbContext
{
    DbConnection? Connection { get; }
    DbTransaction? Transaction { get; }
}