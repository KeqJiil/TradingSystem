using System.Data.Common;
using Microsoft.Data.SqlClient;

namespace Stock.Infrastructure.Persistence.Implementations;

public interface IDbConnectionFactory
{
    DbConnection CreateConnection();
}

public class DbConnectionFactory : IDbConnectionFactory
{
    private readonly IConfiguration _configuration;

    public DbConnectionFactory(IConfiguration configuration)
    {
        _configuration = configuration;
    }

    public DbConnection CreateConnection()
    {
        var connectionString = _configuration.GetConnectionString("DefaultConnection");
        return new SqlConnection(connectionString);
    }
}