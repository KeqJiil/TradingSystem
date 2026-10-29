using Microsoft.EntityFrameworkCore;
using AggregateOrder = Order.Domain.Aggregates.Order;

namespace Order.Infrastructure.Persistence.Configuration;

public class OrderDbContext : DbContext
{
    public DbSet<AggregateOrder> Orders { get; set; }

    public OrderDbContext(DbContextOptions<OrderDbContext> options) : base(options)
    {
    }
    
    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(OrderDbContext).Assembly);
    }
}