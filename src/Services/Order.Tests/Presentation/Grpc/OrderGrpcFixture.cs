using System.Security.Cryptography;
using Grpc.Core;
using Grpc.Net.Client;
using Messaging.Abstractions;
using Messaging.Testing;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;
using Order.Tests.Infrastructure;
using Xunit;
using Proto = TradingSystem.Contracts.ProtobufClasses.Order;

namespace Order.Tests.Presentation.Grpc;

public sealed class OrderGrpcFixture : IAsyncLifetime
{
    private const string Issuer = "order-tests";
    private static readonly byte[] SigningKey = RandomNumberGenerator.GetBytes(32);

    private WebApplicationFactory<Program> _factory = null!;
    private GrpcChannel _channel = null!;

    public MssqlFixture Database { get; } = new();
    public Proto.OrderService.OrderServiceClient Client { get; private set; } = null!;

    public PublishedMessages Published =>
        new(Database.ConnectionString, _factory.Services.GetRequiredService<IMessageSerializerResolver>());

    public async Task InitializeAsync()
    {
        await Database.InitializeAsync();

        _factory = new WebApplicationFactory<Program>().WithWebHostBuilder(builder =>
        {
            builder.UseEnvironment("Testing");
            builder.UseSetting("ConnectionStrings:OrderDbConnectionString", Database.ConnectionString);
            builder.UseSetting("KafkaOptions:BootstrapServers", "localhost:9092");
            builder.UseSetting("KafkaOptions:ProducerClientId", "order-grpc-tests");
            builder.UseSetting("Authentication:Schemes:Bearer:ValidIssuer", Issuer);
            builder.UseSetting("Authentication:Schemes:Bearer:ValidAudiences:0", Issuer);
            builder.UseSetting("Authentication:Schemes:Bearer:SigningKeys:0:Issuer", Issuer);
            builder.UseSetting("Authentication:Schemes:Bearer:SigningKeys:0:Value", Convert.ToBase64String(SigningKey));
            builder.ConfigureTestServices(RemoveMessagingWorkers);
        });

        _channel = GrpcChannel.ForAddress(_factory.Server.BaseAddress,
            new GrpcChannelOptions { HttpHandler = _factory.Server.CreateHandler() });
        Client = new Proto.OrderService.OrderServiceClient(_channel);
    }

    public Metadata AuthFor(Guid userId) => AuthFor(userId.ToString());

    public Metadata AuthFor(string subject) => AuthFor(subject, SigningKey);

    public static Metadata AuthFor(string subject, byte[] key)
    {
        var token = new JsonWebTokenHandler().CreateToken(new SecurityTokenDescriptor
        {
            Issuer = Issuer,
            Audience = Issuer,
            Claims = new Dictionary<string, object> { [JwtRegisteredClaimNames.Sub] = subject },
            Expires = DateTime.UtcNow.AddMinutes(5),
            SigningCredentials = new SigningCredentials(new SymmetricSecurityKey(key), SecurityAlgorithms.HmacSha256)
        });

        return new Metadata { { "authorization", $"Bearer {token}" } };
    }

    public async Task DisposeAsync()
    {
        _channel.Dispose();
        await _factory.DisposeAsync();
        await Database.DisposeAsync();
    }

    private static void RemoveMessagingWorkers(IServiceCollection services)
    {
        var workers = services
            .Where(d => d.ServiceType == typeof(IHostedService))
            .Where(d => (d.ImplementationType ?? d.ImplementationFactory?.Method.DeclaringType)?.Assembly.GetName()
                .Name?.StartsWith("Messaging") == true)
            .ToList();

        foreach (var worker in workers)
            services.Remove(worker);
    }
}
