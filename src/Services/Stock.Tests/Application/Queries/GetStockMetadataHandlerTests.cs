using Dapper;
using Stock.Application.Abstractions;
using Stock.Application.Queries.GetStockMetadata;
using Stock.Infrastructure.Persistence.Implementations;
using Stock.Tests.Infrastructure;
using Xunit;

namespace Stock.Tests.Application.Queries;

public class GetStockMetadataHandlerTests : IClassFixture<MssqlFixture>, IAsyncLifetime
{
    private IStockDataReader Reader { get; init; }
    private GetStockMetadataHandler Handler { get; init; }
    private MssqlFixture MssqlFixture { get; init; }
    private TestDbContext DbContext { get; init; }

    public GetStockMetadataHandlerTests(MssqlFixture mssqlFixture)
    {
        MssqlFixture = mssqlFixture;
        var dbContext = new TestDbContext(MssqlFixture.ConnectionString);
        DbContext = dbContext;
        Reader = new StockDataReader(dbContext);
        Handler = new GetStockMetadataHandler(Reader);
    }

    public async Task InitializeAsync()
    {
        await DbContext.EnsureConnectionOpenAsync(CancellationToken.None);
        await TestDatabase.ResetAsync(DbContext);
    }

    public async Task DisposeAsync()
    {
        await DbContext.Connection.CloseAsync();
    }

    [Fact]
    public async Task Handle_ShouldReturnMetadata_ForExistingStock()
    {
        var stockId = Guid.NewGuid();
        await Seed(stockId, "StockName", true, "USD");

        var result = await Handler.Handle(new GetStockMetadataQuery(stockId), CancellationToken.None);

        Assert.NotNull(result);
        Assert.Equal(stockId, result.Value.Id);
        Assert.Equal("StockName", result.Value.Name);
        Assert.True(result.Value.IsOpenToTrade);
        Assert.Equal("USD", result.Value.Currency);
    }

    [Fact]
    public async Task Handle_ShouldReturnNull_WhenStockDoesNotExist()
    {
        var result = await Handler.Handle(new GetStockMetadataQuery(Guid.NewGuid()), CancellationToken.None);

        Assert.Null(result);
    }

    private async Task Seed(Guid id, string name, bool isOpenToTrade, string currency)
    {
        await DbContext.Connection.ExecuteAsync(
            "INSERT INTO stock_data (id, name, is_open_to_trade, trading_start_time, trading_end_time, currency) VALUES (@Id, @Name, @IsOpenToTrade, @OpenTime, @CloseTime, @Currency)",
            new
            {
                Id = id,
                Name = name,
                IsOpenToTrade = isOpenToTrade,
                OpenTime = TimeSpan.FromHours(9),
                CloseTime = TimeSpan.FromHours(17),
                Currency = currency
            });
    }
}
