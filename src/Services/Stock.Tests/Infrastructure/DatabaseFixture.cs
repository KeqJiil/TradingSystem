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
    private readonly List<string> _emptyDatabases = [];

    public string ConnectionString { get; private set; } = null!;

    public async Task InitializeAsync()
    {
        await ContainerStart.Value;

        ConnectionString = await CreateDatabaseAsync(_databaseName);

        await Task.Run(() => DbMigrator.ApplyMigrations(ConnectionString));
    }

    public async Task<string> CreateEmptyDatabaseAsync()
    {
        var name = $"stock_migration_tests_{Guid.NewGuid():N}";
        _emptyDatabases.Add(name);
        return await CreateDatabaseAsync(name);
    }

    public async Task DisposeAsync()
    {
        SqlConnection.ClearAllPools();
        foreach (var name in _emptyDatabases.Append(_databaseName))
            await ExecuteOnMasterAsync($"""
                                        ALTER DATABASE [{name}] SET SINGLE_USER WITH ROLLBACK IMMEDIATE;
                                        DROP DATABASE [{name}];
                                        """);
    }

    private static async Task<string> CreateDatabaseAsync(string name)
    {
        await ExecuteOnMasterAsync($"CREATE DATABASE [{name}]");

        return new SqlConnectionStringBuilder(SharedContainer.GetConnectionString())
        {
            InitialCatalog = name
        }.ConnectionString;
    }

    private static async Task ExecuteOnMasterAsync(string sql)
    {
        await using var connection = new SqlConnection(SharedContainer.GetConnectionString());
        await connection.OpenAsync();
        await using var command = new SqlCommand(sql, connection);
        await command.ExecuteNonQueryAsync();
    }
}
