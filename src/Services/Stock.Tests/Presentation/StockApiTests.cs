using System.Net;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using Dapper;
using Stock.Tests.Infrastructure;
using Xunit;

namespace Stock.Tests.Presentation;

public class StockApiTests : IClassFixture<StockApiFixture>, IAsyncLifetime
{
    private const string Stocks = "/api/v1/stocks";

    private readonly StockApiFixture _fixture;
    private readonly HttpClient _client;
    private TestDbContext _dbContext = null!;

    public StockApiTests(StockApiFixture fixture)
    {
        _fixture = fixture;
        _client = fixture.Client;
    }

    public async Task InitializeAsync()
    {
        _dbContext = new TestDbContext(_fixture.ConnectionString);
        await _dbContext.EnsureConnectionOpenAsync(CancellationToken.None);
    }

    public async Task DisposeAsync()
    {
        await _dbContext.DisposeAsync();
    }

    public static TheoryData<string, string, string> UpdateRequests => new()
    {
        { "PATCH", "name", """{"name":"NewName"}""" },
        { "PATCH", "trading-time", """{"openTime":"10:00:00","closeTime":"18:00:00"}""" },
        { "PUT", "open-to-trade", """{"isOpenToTrade":false}""" }
    };

    private static readonly Dictionary<string, (string Property, string Value)> ExpectedMetadata = new()
    {
        ["name"] = ("name", "NewName"),
        ["trading-time"] = ("tradingStartTime", "10:00:00"),
        ["open-to-trade"] = ("isOpenToTrade", "False")
    };

    [Fact]
    public async Task CreateStock_Returns201WithLocation_ThatServesTheStoredMetadata_AndWritesOneEvent()
    {
        var id = Guid.NewGuid();

        var response = await CreateStock(id);

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        Assert.Equal($"{Stocks}/{id}/metadata", response.Headers.Location!.OriginalString);
        Assert.Equal(id, (await ReadJson(response)).GetProperty("id").GetGuid());

        var metadata = await ReadJson(await _client.GetAsync(response.Headers.Location));
        Assert.Equal("AAPL", metadata.GetProperty("name").GetString());
        Assert.Equal("USD", metadata.GetProperty("currency").GetString());
        Assert.True(metadata.GetProperty("isOpenToTrade").GetBoolean());
        Assert.Equal("09:00:00", metadata.GetProperty("tradingStartTime").GetString());
        Assert.Equal("17:00:00", metadata.GetProperty("tradingEndTime").GetString());
        Assert.Equal(1, await OutboxEvents(id));
    }

    [Fact]
    public async Task CreateStock_Repeated_WithSameBody_Returns200_WithoutSecondEvent()
    {
        var id = Guid.NewGuid();
        await CreateStock(id);

        var response = await CreateStock(id);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(1, await OutboxEvents(id));
    }

    [Fact]
    public async Task CreateStock_Repeated_WithDifferentBody_Returns409_AndKeepsOriginal()
    {
        var id = Guid.NewGuid();
        await CreateStock(id);

        var response = await CreateStock(id, name: "MSFT");

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        var metadata = await ReadJson(await _client.GetAsync($"{Stocks}/{id}/metadata"));
        Assert.Equal("AAPL", metadata.GetProperty("name").GetString());
        Assert.Equal(1, await OutboxEvents(id));
    }

    [Theory]
    [InlineData("name", "", "Name")]
    [InlineData("name", "AAAAAAAAAAAAAAAAAAAAA", "Name")]
    [InlineData("currency", "usd", "Currency")]
    [InlineData("currency", "US", "Currency")]
    [InlineData("tradingEndTime", "09:00:00", "TradingEndTime")]
    [InlineData("tradingEndTime", "08:00:00", "TradingEndTime")]
    public async Task CreateStock_WithInvalidField_Returns400ProblemDetails_AndStoresNothing(string field,
        string value, string errorKey)
    {
        var id = Guid.NewGuid();
        var body = new Dictionary<string, object>
        {
            ["name"] = "AAPL",
            ["isOpenToTrade"] = true,
            ["currency"] = "USD",
            ["tradingStartTime"] = "09:00:00",
            ["tradingEndTime"] = "17:00:00",
            [field] = value
        };

        var response = await _client.PutAsJsonAsync($"{Stocks}/{id}", body);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType!.MediaType);
        var problem = await ReadJson(response);
        Assert.Equal(400, problem.GetProperty("status").GetInt32());
        Assert.True(problem.GetProperty("errors").TryGetProperty(errorKey, out var messages));
        Assert.NotEmpty(messages.EnumerateArray());
        Assert.Equal(response.Headers.GetValues("X-Correlation-ID").Single(),
            problem.GetProperty("correlationId").GetString());
        Assert.Equal(0, await _dbContext.Connection.ExecuteScalarAsync<int>(
            "SELECT COUNT(*) FROM stock_data WHERE id = @Id", new { Id = id }));
    }

    [Theory]
    [InlineData("{}")]
    [InlineData("""{"name":null,"isOpenToTrade":true,"currency":"USD","tradingStartTime":"09:00:00","tradingEndTime":"17:00:00"}""")]
    [InlineData("not json")]
    [InlineData("")]
    public async Task CreateStock_WithMalformedOrIncompleteBody_Returns400(string body)
    {
        var id = Guid.NewGuid();

        var response = await _client.PutAsync($"{Stocks}/{id}", Json(body));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal(0, await _dbContext.Connection.ExecuteScalarAsync<int>(
            "SELECT COUNT(*) FROM stock_data WHERE id = @Id", new { Id = id }));
    }

    [Fact]
    public async Task CreateStock_WithNonJsonContentType_Returns415()
    {
        var response = await _client.PutAsync($"{Stocks}/{Guid.NewGuid()}",
            new StringContent("{}", Encoding.UTF8, "text/plain"));

        Assert.Equal(HttpStatusCode.UnsupportedMediaType, response.StatusCode);
    }

    [Fact]
    public async Task Routes_WithNonGuidId_Return404()
    {
        Assert.Equal(HttpStatusCode.NotFound, (await _client.GetAsync($"{Stocks}/not-a-guid")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound,
            (await _client.PutAsync($"{Stocks}/not-a-guid", Json("{}"))).StatusCode);
    }

    [Theory]
    [MemberData(nameof(UpdateRequests))]
    public async Task Update_Returns204_AndIsVisibleInMetadata(string method, string route, string body)
    {
        var id = Guid.NewGuid();
        await CreateStock(id);

        var response = await Send(method, $"{Stocks}/{id}/{route}", body);

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
        var metadata = await ReadJson(await _client.GetAsync($"{Stocks}/{id}/metadata"));
        var (property, expected) = ExpectedMetadata[route];
        Assert.Equal(expected, metadata.GetProperty(property).ToString());
        Assert.Equal(2, await OutboxEvents(id));
    }

    [Theory]
    [MemberData(nameof(UpdateRequests))]
    public async Task Update_OfUnknownStock_Returns404_AndWritesNoEvent(string method, string route, string body)
    {
        var id = Guid.NewGuid();

        var response = await Send(method, $"{Stocks}/{id}/{route}", body);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        Assert.Equal(0, await OutboxEvents(id));
    }

    [Theory]
    [InlineData("PATCH", "name", """{"name":""}""", "Name")]
    [InlineData("PATCH", "name", """{"name":"AAAAAAAAAAAAAAAAAAAAA"}""", "Name")]
    [InlineData("PATCH", "trading-time", """{"openTime":"18:00:00","closeTime":"10:00:00"}""", "CloseTime")]
    public async Task Update_WithInvalidBody_Returns400ProblemDetails(string method, string route, string body,
        string errorKey)
    {
        var response = await Send(method, $"{Stocks}/{Guid.NewGuid()}/{route}", body);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.True((await ReadJson(response)).GetProperty("errors").TryGetProperty(errorKey, out _));
    }

    [Fact]
    public async Task Update_WithEmptyGuid_Returns400()
    {
        var response = await Send("PUT", $"{Stocks}/{Guid.Empty}/open-to-trade", """{"isOpenToTrade":true}""");

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.True((await ReadJson(response)).GetProperty("errors").TryGetProperty("Id", out _));
    }

    [Fact]
    public async Task GetStock_ReturnsProjection_AndNotFoundForUnknownStock()
    {
        var id = Guid.NewGuid();
        await ReadModelSeed.Projection(_dbContext, id);

        var found = await _client.GetAsync($"{Stocks}/{id}");
        var missing = await _client.GetAsync($"{Stocks}/{Guid.NewGuid()}");

        Assert.Equal(HttpStatusCode.OK, found.StatusCode);
        var stock = await ReadJson(found);
        Assert.Equal(id, stock.GetProperty("aggregateId").GetGuid());
        Assert.Equal("StockName", stock.GetProperty("name").GetString());
        Assert.Equal(0m, stock.GetProperty("price").GetDecimal());
        Assert.Equal("USD", stock.GetProperty("currency").GetString());
        Assert.Equal(HttpStatusCode.NotFound, missing.StatusCode);
        Assert.Equal("application/problem+json", missing.Content.Headers.ContentType!.MediaType);
    }

    [Fact]
    public async Task GetMetadata_OfUnknownStock_Returns404()
    {
        var response = await _client.GetAsync($"{Stocks}/{Guid.NewGuid()}/metadata");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task GetHourlyPrices_ReturnsCandlesOfRange_AndEmptyListWhenNoData()
    {
        var id = Guid.NewGuid();
        await SeedHour(id, new DateTimeOffset(2026, 3, 5, 9, 0, 0, TimeSpan.Zero), open: 10m, close: 12m);
        await SeedHour(id, new DateTimeOffset(2026, 3, 5, 10, 0, 0, TimeSpan.Zero), open: 12m, close: 11m);
        await SeedHour(id, new DateTimeOffset(2026, 3, 5, 17, 0, 0, TimeSpan.Zero), open: 99m, close: 99m);

        var response = await _client.GetAsync(
            $"{Stocks}/{id}/prices/hourly?from=2026-03-05T09:00:00Z&to=2026-03-05T17:00:00Z");
        var empty = await _client.GetAsync(
            $"{Stocks}/{Guid.NewGuid()}/prices/hourly?from=2026-03-05T09:00:00Z&to=2026-03-05T17:00:00Z");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var candles = (await ReadJson(response)).GetProperty("priceChanges").EnumerateArray().ToList();
        Assert.Equal([10m, 12m], candles.Select(c => c.GetProperty("openPrice").GetDecimal()));
        Assert.Equal(HttpStatusCode.OK, empty.StatusCode);
        Assert.Empty((await ReadJson(empty)).GetProperty("priceChanges").EnumerateArray());
    }

    [Theory]
    [InlineData("from=2026-03-05T09:00:00Z&to=2026-03-13T09:00:01Z")]
    [InlineData("from=2026-03-05T09:00:00Z&to=2026-03-05T09:00:00Z")]
    [InlineData("from=2026-03-05T09:00:00Z&to=2026-03-05T08:00:00Z")]
    public async Task GetHourlyPrices_WithInvalidRange_Returns400OnTo(string query)
    {
        var response = await _client.GetAsync($"{Stocks}/{Guid.NewGuid()}/prices/hourly?{query}");

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.True((await ReadJson(response)).GetProperty("errors").TryGetProperty("To", out _));
    }

    [Theory]
    [InlineData("from=2026-03-05T09:00:00Z")]
    [InlineData("to=2026-03-05T09:00:00Z")]
    [InlineData("from=yesterday&to=today")]
    public async Task GetHourlyPrices_WithMissingOrUnparsableQuery_Returns400(string query)
    {
        var response = await _client.GetAsync($"{Stocks}/{Guid.NewGuid()}/prices/hourly?{query}");

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task GetDailyPrice_ReturnsCandle_404ForMissingDay_400ForBadDate()
    {
        var id = Guid.NewGuid();
        await SeedDay(id, new DateOnly(2026, 3, 5), open: 10m, close: 15m);

        var found = await _client.GetAsync($"{Stocks}/{id}/prices/daily/2026-03-05");
        var missing = await _client.GetAsync($"{Stocks}/{id}/prices/daily/2026-03-06");
        var invalid = await _client.GetAsync($"{Stocks}/{id}/prices/daily/not-a-date");

        Assert.Equal(HttpStatusCode.OK, found.StatusCode);
        var candle = await ReadJson(found);
        Assert.Equal(10m, candle.GetProperty("openPrice").GetDecimal());
        Assert.Equal(15m, candle.GetProperty("closePrice").GetDecimal());
        Assert.Equal(HttpStatusCode.NotFound, missing.StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, invalid.StatusCode);
    }

    [Fact]
    public async Task GetDailyPrices_ReturnsCandlesOfRange_AndEmptyListWhenNoData()
    {
        var id = Guid.NewGuid();
        await SeedDay(id, new DateOnly(2026, 3, 1), open: 1m, close: 2m);
        await SeedDay(id, new DateOnly(2026, 3, 9), open: 3m, close: 4m);
        await SeedDay(id, new DateOnly(2026, 3, 10), open: 99m, close: 99m);

        var response = await _client.GetAsync($"{Stocks}/{id}/prices/daily?from=2026-03-01&to=2026-03-10");
        var empty = await _client.GetAsync($"{Stocks}/{Guid.NewGuid()}/prices/daily?from=2026-03-01&to=2026-03-10");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var candles = (await ReadJson(response)).GetProperty("dailyReadModels").EnumerateArray().ToList();
        Assert.Equal([1m, 3m], candles.Select(c => c.GetProperty("openPrice").GetDecimal()).Order());
        Assert.Empty((await ReadJson(empty)).GetProperty("dailyReadModels").EnumerateArray());
    }

    [Theory]
    [InlineData("from=2026-03-01&to=2027-03-03")]
    [InlineData("from=2026-03-01&to=2026-03-01")]
    [InlineData("from=2026-03-10&to=2026-03-01")]
    public async Task GetDailyPrices_WithInvalidRange_Returns400OnTo(string query)
    {
        var response = await _client.GetAsync($"{Stocks}/{Guid.NewGuid()}/prices/daily?{query}");

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.True((await ReadJson(response)).GetProperty("errors").TryGetProperty("To", out _));
    }

    [Fact]
    public async Task Response_AlwaysCarriesCorrelationId_EchoingValidOnesAndReplacingInvalidOnes()
    {
        var supplied = Guid.NewGuid();
        var withoutHeader = await _client.GetAsync($"{Stocks}/{Guid.NewGuid()}/metadata");
        var withValid = await GetWithCorrelation($"{Stocks}/{Guid.NewGuid()}/metadata", supplied.ToString());
        var withGarbage = await GetWithCorrelation($"{Stocks}/{Guid.NewGuid()}/metadata", "not-a-guid");

        Assert.True(Guid.TryParse(withoutHeader.Headers.GetValues("X-Correlation-ID").Single(), out _));
        Assert.Equal(supplied.ToString(), withValid.Headers.GetValues("X-Correlation-ID").Single());
        Assert.True(Guid.TryParse(withGarbage.Headers.GetValues("X-Correlation-ID").Single(), out _));
        Assert.NotEqual("not-a-guid", withGarbage.Headers.GetValues("X-Correlation-ID").Single());
    }

    [Fact]
    public async Task ProblemDetails_CarryTheSameCorrelationIdAsTheHeader()
    {
        var supplied = Guid.NewGuid();

        var response = await GetWithCorrelation($"{Stocks}/{Guid.NewGuid()}/metadata", supplied.ToString());

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        Assert.Equal(supplied, (await ReadJson(response)).GetProperty("correlationId").GetGuid());
    }

    [Fact]
    public async Task CorrelationId_OfCreateRequest_IsStoredOnTheOutboxEvent()
    {
        var id = Guid.NewGuid();
        var supplied = Guid.NewGuid();
        using var request = new HttpRequestMessage(HttpMethod.Put, $"{Stocks}/{id}")
        {
            Content = JsonContent.Create(StockBody("AAPL"))
        };
        request.Headers.Add("X-Correlation-ID", supplied.ToString());

        await _client.SendAsync(request);

        Assert.Equal(supplied, await _dbContext.Connection.ExecuteScalarAsync<Guid?>(
            "SELECT correlation_id FROM outbox WHERE aggregate_id = @Id", new { Id = id }));
    }

    [Fact]
    public async Task UnknownRoute_Returns404ProblemDetails()
    {
        var response = await _client.GetAsync("/api/v1/nothing-here");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType!.MediaType);
    }

    private static object StockBody(string name)
    {
        return new
        {
            name,
            isOpenToTrade = true,
            currency = "USD",
            tradingStartTime = "09:00:00",
            tradingEndTime = "17:00:00"
        };
    }

    private static StringContent Json(string body)
    {
        return new StringContent(body, Encoding.UTF8, "application/json");
    }

    private static async Task<JsonElement> ReadJson(HttpResponseMessage response)
    {
        return await response.Content.ReadFromJsonAsync<JsonElement>();
    }

    private Task<HttpResponseMessage> CreateStock(Guid id, string name = "AAPL")
    {
        return _client.PutAsJsonAsync($"{Stocks}/{id}", StockBody(name));
    }

    private Task<HttpResponseMessage> Send(string method, string url, string body)
    {
        return _client.SendAsync(new HttpRequestMessage(new HttpMethod(method), url) { Content = Json(body) });
    }

    private Task<HttpResponseMessage> GetWithCorrelation(string url, string correlationId)
    {
        var request = new HttpRequestMessage(HttpMethod.Get, url);
        request.Headers.Add("X-Correlation-ID", correlationId);
        return _client.SendAsync(request);
    }

    private Task<int> OutboxEvents(Guid aggregateId)
    {
        return _dbContext.Connection.ExecuteScalarAsync<int>(
            "SELECT COUNT(*) FROM outbox WHERE aggregate_id = @Id", new { Id = aggregateId });
    }

    private Task SeedHour(Guid aggregateId, DateTimeOffset hourStart, decimal open, decimal close)
    {
        return _dbContext.Connection.ExecuteAsync("""
            INSERT INTO hourly_stock_data_projection
                (id, aggregate_id, open_price, low_price, high_price, close_price, price_difference, last_version, date_time)
            VALUES
                (NEWID(), @AggregateId, @Open, @Low, @High, @Close, @Close - @Open, 1, @HourStart)
            """,
            new
            {
                AggregateId = aggregateId,
                Open = open,
                Low = Math.Min(open, close),
                High = Math.Max(open, close),
                Close = close,
                HourStart = hourStart
            });
    }

    private Task SeedDay(Guid aggregateId, DateOnly date, decimal open, decimal close)
    {
        return _dbContext.Connection.ExecuteAsync("""
            INSERT INTO daily_stock_data_projection
                (id, aggregate_id, open_price, low_price, high_price, close_price, price_difference, last_version, date)
            VALUES
                (NEWID(), @AggregateId, @Open, @Low, @High, @Close, @Close - @Open, 1, @Date)
            """,
            new
            {
                AggregateId = aggregateId,
                Open = open,
                Low = Math.Min(open, close),
                High = Math.Max(open, close),
                Close = close,
                Date = date
            });
    }
}
