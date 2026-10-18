using Microsoft.EntityFrameworkCore;

namespace Messaging.EFCore.Tests.Infrastructure;

public sealed class MssqlFixture : MssqlFixtureBase
{
    public override async Task CreateSchemaAsync(string connectionString)
    {
        await using var context = CreateContext(connectionString);
        await context.Database.EnsureCreatedAsync();
    }

    public TestDbContext CreateContext(string? connectionString = null) =>
        new(new DbContextOptionsBuilder<TestDbContext>().UseSqlServer(connectionString ?? ConnectionString).Options);
}
