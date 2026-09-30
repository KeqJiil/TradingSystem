using Stock.Tests.Infrastructure;
using Xunit;

namespace Stock.Tests.Presentation;

public class StockApiFixture : IAsyncLifetime
{
    private readonly MssqlFixture _mssql = new();
    private StockApiFactory _factory = null!;

    public string ConnectionString => _mssql.ConnectionString;
    public HttpClient Client { get; private set; } = null!;

    public async Task InitializeAsync()
    {
        await _mssql.InitializeAsync();
        _factory = new StockApiFactory(_mssql.ConnectionString, "localhost:1");
        Client = _factory.CreateApiClient();
    }

    public async Task DisposeAsync()
    {
        Client.Dispose();
        await _factory.DisposeAsync();
        await _mssql.DisposeAsync();
    }
}
