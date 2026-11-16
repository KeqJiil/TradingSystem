using Messaging.EFCore.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Messaging.EFCore.Tests.Infrastructure;

public class TestDbContext(DbContextOptions<TestDbContext> options) : DbContext(options)
{
    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.RegisterOutbox();
        modelBuilder.RegisterInbox();
        modelBuilder.Entity<ProbeEntity>(b =>
        {
            b.ToTable("probe");
            b.HasKey(x => x.Key);
            b.Property(x => x.Key).HasMaxLength(100);
        });
    }
}
