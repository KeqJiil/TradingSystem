using System.Net;
using Stock.Tests.Infrastructure;
using Xunit;

namespace Stock.Tests.Presentation;

public class HealthApiTests : IClassFixture<MssqlFixture>, IClassFixture<KafkaFixture>
{
    private readonly MssqlFixture _mssql;
    private readonly KafkaFixture _kafka;

    public HealthApiTests(MssqlFixture mssql, KafkaFixture kafka)
    {
        _mssql = mssql;
        _kafka = kafka;
    }

    [Fact]
    public async Task Live_IsHealthy_EvenWhenKafkaIsUnreachable()
    {
        await using var factory = new StockApiFactory(_mssql.ConnectionString, "localhost:1");
        using var client = factory.CreateApiClient();

        var response = await client.GetAsync("/health/live");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task Ready_IsHealthy_WhenSqlAndKafkaAreUp()
    {
        await using var factory = new StockApiFactory(_mssql.ConnectionString, _kafka.BootstrapAddress);
        using var client = factory.CreateApiClient();

        var response = await client.GetAsync("/health/ready");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task Ready_IsUnhealthy_WhenKafkaIsUnreachable()
    {
        await using var factory = new StockApiFactory(_mssql.ConnectionString, "localhost:1");
        using var client = factory.CreateApiClient();

        var response = await client.GetAsync("/health/ready");

        Assert.Equal(HttpStatusCode.ServiceUnavailable, response.StatusCode);
    }
}
