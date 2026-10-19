using Dapper;
using MediatR;
using Messaging;
using Messaging.Testing;
using Microsoft.Extensions.DependencyInjection;
using Stock.Application.Commands.CreateStock;
using Stock.Application.Events;
using Stock.Infrastructure.Topics;
using Stock.Tests.Infrastructure;
using Xunit;

namespace Stock.Tests.Application.Commands;

public class CreateStockHandlerTests(MssqlFixture fixture) : IClassFixture<MssqlFixture>, IAsyncLifetime
{
    private readonly TestDbContext _db = new(fixture.ConnectionString);
    private ServiceProvider _provider = null!;

    private PublishedMessages Published => _provider.GetRequiredService<PublishedMessages>();

    public async Task InitializeAsync()
    {
        _provider = BuildProvider();
        await _db.EnsureConnectionOpenAsync(CancellationToken.None);
        await TestDatabase.ResetAsync(_db);
    }

    public async Task DisposeAsync()
    {
        await _provider.DisposeAsync();
        await _db.DisposeAsync();
    }

    [Fact]
    public async Task Handle_NewStock_CreatesStockAndPublishesStockCreated()
    {
        var command = NewCommand(Guid.NewGuid());

        Assert.Equal(CreateStockResult.Created, await SendAsync(_provider, command));

        var stock = await _db.Connection.QuerySingleAsync(
            "SELECT name, currency FROM stock_data WHERE id = @Id", new { command.Id });
        Assert.Equal(command.Name, stock.name);
        Assert.Equal(command.Currency, stock.currency);
        var published = Assert.Single(await Published.OfTypeAsync<StockCreatedEvent>(command.Id.ToString()));
        Assert.Equal(StockTopic.StockCreated.Name(), published.Topic);
        Assert.Equal(new StockCreatedEvent(command.Id, command.Name, command.IsOpenToTrade, command.Currency,
            command.TradingStartTime, command.TradingEndTime), published.Message);
    }

    [Fact]
    public async Task Handle_RepeatedWithSameBody_ReturnsAlreadyExistsAndPublishesOnce()
    {
        var command = NewCommand(Guid.NewGuid());

        await SendAsync(_provider, command);

        Assert.Equal(CreateStockResult.AlreadyExists, await SendAsync(_provider, command));
        Assert.Single(await Published.OfTypeAsync<StockCreatedEvent>(command.Id.ToString()));
    }

    [Fact]
    public async Task Handle_RepeatedWithDifferentBody_ReturnsConflictAndKeepsOriginal()
    {
        var command = NewCommand(Guid.NewGuid());

        await SendAsync(_provider, command);

        Assert.Equal(CreateStockResult.Conflict, await SendAsync(_provider, command with { Currency = "EUR" }));
        Assert.Equal("USD", await _db.Connection.ExecuteScalarAsync<string>(
            "SELECT currency FROM stock_data WHERE id = @Id", new { command.Id }));
        Assert.Single(await Published.OfTypeAsync<StockCreatedEvent>(command.Id.ToString()));
    }

    [Fact]
    public async Task Handle_PublishFails_DoesNotCreateStock()
    {
        var command = NewCommand(Guid.NewGuid());
        await using var failing = BuildProvider(b => b.AddPublishMiddleware<FailingPublishMiddleware>());

        await Assert.ThrowsAsync<InvalidOperationException>(() => SendAsync(failing, command));

        Assert.Equal(0, await _db.Connection.ExecuteScalarAsync<int>(
            "SELECT COUNT(*) FROM stock_data WHERE id = @Id", new { command.Id }));
    }

    private static CreateStockCommand NewCommand(Guid id)
    {
        return new CreateStockCommand(id, "StockName", true, "USD", new TimeOnly(9, 0), new TimeOnly(17, 0));
    }

    private ServiceProvider BuildProvider(Action<IMessagingBuilder>? configure = null)
    {
        return StockServices.Build(fixture.ConnectionString, configure);
    }

    private static async Task<CreateStockResult> SendAsync(IServiceProvider provider, CreateStockCommand command)
    {
        await using var scope = provider.CreateAsyncScope();
        return await scope.ServiceProvider.GetRequiredService<IMediator>().Send(command);
    }
}
