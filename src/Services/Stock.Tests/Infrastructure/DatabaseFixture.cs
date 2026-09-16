using Microsoft.Data.SqlClient;
using Stock.Infrastructure.Persistence;
using Testcontainers.MsSql;
using Xunit;

namespace Stock.Tests.Infrastructure;

public class MssqlFixture : IAsyncLifetime
{
    private static readonly MsSqlContainer SharedContainer =
        new MsSqlBuilder("mcr.microsoft.com/mssql/server:2022-latest")
            .WithPassword("St0ckP@ssw0rd")
            .Build();

    private static readonly Lazy<Task> ContainerStart = new(() => SharedContainer.StartAsync());

    private readonly string _databaseName = $"stock_tests_{Guid.NewGuid():N}";

    public string ConnectionString { get; private set; } = null!;

    public async Task InitializeAsync()
    {
        await ContainerStart.Value;

        await ExecuteOnMasterAsync($"CREATE DATABASE [{_databaseName}]");

        ConnectionString = new SqlConnectionStringBuilder(SharedContainer.GetConnectionString())
        {
            InitialCatalog = _databaseName
        }.ConnectionString;

        await Task.Run(() => DbMigrator.ApplyMigrations(ConnectionString));
    }

    public async Task DisposeAsync()
    {
        SqlConnection.ClearAllPools();
        await ExecuteOnMasterAsync($"""
                                    ALTER DATABASE [{_databaseName}] SET SINGLE_USER WITH ROLLBACK IMMEDIATE;
                                    DROP DATABASE [{_databaseName}];
                                    """);
    }

    private static async Task ExecuteOnMasterAsync(string sql)
    {
        await using var connection = new SqlConnection(SharedContainer.GetConnectionString());
        await connection.OpenAsync();
        await using var command = new SqlCommand(sql, connection);
        await command.ExecuteNonQueryAsync();
    }
}
