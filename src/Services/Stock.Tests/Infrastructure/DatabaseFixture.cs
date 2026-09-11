using Testcontainers.MsSql;
using Xunit;

namespace Stock.Tests.Infrastructure;

public class MssqlFixture : IAsyncLifetime
{
    private readonly MsSqlContainer _mssqlContainer = new MsSqlBuilder("mcr.microsoft.com/mssql/server:2022-latest")
        .WithPassword("St0ckP@ssw0rd")
        .Build();

    public string ConnectionString => _mssqlContainer.GetConnectionString();

    public async Task InitializeAsync()
    {
        await _mssqlContainer.StartAsync();
    }

    public async Task DisposeAsync()
    {
        await _mssqlContainer.DisposeAsync();
    }
}