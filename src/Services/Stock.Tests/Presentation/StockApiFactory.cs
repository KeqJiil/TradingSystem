using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Stock.Tests.Presentation;

public sealed class StockApiFactory(string sqlConnectionString, string kafkaBootstrapServers)
    : WebApplicationFactory<Program>
{
    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Production");
        builder.UseSetting("ConnectionStrings:DefaultConnection", sqlConnectionString);
        builder.UseSetting("KafkaOptions:BootstrapServers", kafkaBootstrapServers);
        builder.UseSetting("KafkaOptions:ProducerClientId", "stock-api-tests");
        builder.ConfigureLogging(logging => logging.SetMinimumLevel(LogLevel.Warning));
        builder.ConfigureTestServices(services => services.RemoveAll<IHostedService>());
    }

    public HttpClient CreateApiClient()
    {
        return CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });
    }
}
