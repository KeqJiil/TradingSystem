using System.Data.Common;
using Microsoft.Data.SqlClient;
using Stock.Infrastructure.Persistence.Implementations;

namespace Stock.Tests.Infrastructure;

public class TestDbConnectionFactory(string connectionString) : IDbConnectionFactory
{
    public DbConnection CreateConnection() => new SqlConnection(connectionString);
}
