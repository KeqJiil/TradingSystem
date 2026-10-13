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

    private readonly List<string> _databases = [];

    public string ConnectionString { get; private set; } = null!;

    public static MigrationScript[] LibraryScripts =>
        [EmbeddedMigration.Read("001_Inbox.sql"), EmbeddedMigration.Read("002_Outbox.sql")];

    public async Task InitializeAsync()
    {
        await ContainerStart.Value;

        ConnectionString = NewDatabaseConnectionString();

        await Task.Run(() =>
            DbMigrator.ApplyMigrations(ConnectionString, typeof(MssqlFixture).Assembly, LibraryScripts));
    }

    public string NewDatabaseConnectionString()
    {
        var name = $"messaging_tests_{Guid.NewGuid():N}";
        _databases.Add(name);

        return new SqlConnectionStringBuilder(SharedContainer.GetConnectionString())
        {
            InitialCatalog = name
        }.ConnectionString;
    }

    public async Task<SqlConnection> OpenConnectionAsync(string? connectionString = null)
    {
        var connection = new SqlConnection(connectionString ?? ConnectionString);
        await connection.OpenAsync();
        return connection;
    }

    public async Task DisposeAsync()
    {
        SqlConnection.ClearAllPools();

        await using var connection = new SqlConnection(SharedContainer.GetConnectionString());
        await connection.OpenAsync();

        foreach (var name in _databases)
        {
            await using var command = new SqlCommand($"""
                                                      IF DB_ID('{name}') IS NOT NULL
                                                      BEGIN
                                                          ALTER DATABASE [{name}] SET SINGLE_USER WITH ROLLBACK IMMEDIATE;
                                                          DROP DATABASE [{name}];
                                                      END
                                                      """, connection);
            await command.ExecuteNonQueryAsync();
        }
    }
}
