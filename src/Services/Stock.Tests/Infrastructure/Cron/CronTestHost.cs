using Dapper;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Internal;
using Stock.Infrastructure.Messaging;
using Stock.Infrastructure.Persistence;
using Stock.Presentation.Builder;

namespace Stock.Tests.Infrastructure.Cron;

internal static class CronTestHost
{
    public static async Task RunAsync<TWorker>(string connectionString, ISystemClock clock,
        Func<TWorker, Task> execute) where TWorker : notnull
    {
        var builder = WebApplication.CreateBuilder();
        builder.Configuration.AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["ConnectionStrings:DefaultConnection"] = connectionString
        });
        builder.AddResilence();
        builder.AddPersistence();
        builder.AddApplication();
        builder.AddMessaging();
        builder.Services.AddSingleton(clock);

        await using var provider = builder.Services.BuildServiceProvider();
        await using var scope = provider.CreateAsyncScope();
        await execute(scope.ServiceProvider.GetRequiredService<TWorker>());
    }

    public static async Task<List<Guid>> SeedStocksAsync(TestDbContext dbContext, int count)
    {
        var ids = Enumerable.Range(0, count).Select(_ => Guid.NewGuid()).ToList();

        await dbContext.Connection.ExecuteAsync(
            "INSERT INTO stock_data (id, name, is_open_to_trade, trading_start_time, trading_end_time, currency) VALUES (@Id, 'Stock', 1, '09:00', '17:00', 'USD')",
            ids.Select(id => new { Id = id }));

        return ids;
    }
}
