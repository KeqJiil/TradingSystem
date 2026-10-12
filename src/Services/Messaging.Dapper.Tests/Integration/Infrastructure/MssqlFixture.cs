using Messaging.Dapper.Migrations;
using Microsoft.Data.SqlClient;
using Testcontainers.MsSql;

namespace Messaging.Dapper.Tests.Integration.Infrastructure;

public sealed class MssqlFixture : IAsyncLifetime
{
    private static readonly MsSqlContainer SharedContainer =
        new MsSqlBuilder("mcr.microsoft.com/mssql/server:2022-latest")
            .WithPassword("M3ssag1ng@Tests")
            .Build();

    private static readonly Lazy<Task> ContainerStart = new(() => SharedContainer.StartAsync());

    private readonly string _databaseName = $"messaging_tests_{Guid.NewGuid():N}";

    public string ConnectionString { get; private set; } = null!;

    public async Task InitializeAsync()
    {
        await ContainerStart.Value;

        ConnectionString = new SqlConnectionStringBuilder(SharedContainer.GetConnectionString())
        {
            InitialCatalog = _databaseName
        }.ConnectionString;

        await Task.Run(() => DbMigrator.ApplyMigrations(ConnectionString, typeof(MssqlFixture).Assembly,
            [EmbeddedMigration.Read("001_Inbox.sql"), EmbeddedMigration.Read("002_Outbox.sql")]));
    }

    public async Task<SqlConnection> OpenConnectionAsync()
    {
        var connection = new SqlConnection(ConnectionString);
        await connection.OpenAsync();
        return connection;
    }

    public async Task DisposeAsync()
    {
        SqlConnection.ClearAllPools();

        await using var connection = new SqlConnection(SharedContainer.GetConnectionString());
        await connection.OpenAsync();
        await using var command = new SqlCommand($"""
                                                  ALTER DATABASE [{_databaseName}] SET SINGLE_USER WITH ROLLBACK IMMEDIATE;
                                                  DROP DATABASE [{_databaseName}];
                                                  """, connection);
        await command.ExecuteNonQueryAsync();
    }
}
