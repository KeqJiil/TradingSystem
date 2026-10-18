using Messaging.EFCore.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Messaging.EFCore.Tests.Infrastructure;

public class TestDbContext(DbContextOptions<TestDbContext> options) : DbContext(options)
{
    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.RegisterOutbox();
        modelBuilder.RegisterInbox();
    }
}
