using Messaging.Testing;
using Microsoft.EntityFrameworkCore;
using Order.Infrastructure.Persistence.Configuration;

namespace Order.Tests.Infrastructure;

public sealed class MssqlFixture : MssqlFixtureBase
{
    public override async Task CreateSchemaAsync(string connectionString)
    {
        await using var context = CreateContext(connectionString);
        await context.Database.MigrateAsync();
    }

    public OrderDbContext CreateContext(string? connectionString = null) =>
        new(new DbContextOptionsBuilder<OrderDbContext>().UseSqlServer(connectionString ?? ConnectionString).Options);
}
