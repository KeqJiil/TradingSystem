using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Order.Domain.VO;
using AggregateOrder = Order.Domain.Aggregates.Order;

namespace Order.Infrastructure.Persistence.Configuration;

public class OrderConfiguration : IEntityTypeConfiguration<AggregateOrder>
{
    public void Configure(EntityTypeBuilder<AggregateOrder> builder)
    {
        builder.ToTable("order", "order");

        builder.HasKey(x => x.Id).HasName("pk_order");
        builder.Property(x => x.Id).HasColumnName("id");

        builder.OwnsMany(x => x.Fills, f =>
        {
            f.ToTable("fill", "order");

            f.WithOwner().HasForeignKey(x => x.OrderId);
            f.Property(c => c.OrderId).HasColumnName("order_id");

            f.Property<long>("id");
            f.HasKey("id");

            f.Property(c => c.ExecutionId).HasColumnName("execution_id");
            f.Property(c => c.FilledAt).HasColumnName("filled_at");
            f.Property(c => c.Quantity).HasConversion(a => a.Value, b => Quantity.Create(b).Value)
                .HasColumnName("quantity");

            f.Property(c => c.Price).HasConversion(a => a.Value, b => Price.Create(b).Value).HasPrecision(18, 2)
                .HasColumnName("price");

            f.HasIndex(c => c.ExecutionId).HasDatabaseName("fill_execution_id_idx").IsUnique();
        });

        builder.Property(x => x.UserId).HasColumnName("user_id");

        builder.Property(x => x.FilledQuantity).HasColumnName("filled_quantity");

        builder.Property(x => x.Symbol).HasConversion<string>().HasMaxLength(20).HasColumnName("symbol");
        builder.Property(x => x.Quantity).HasConversion(a => a.Value, b => Quantity.Create(b).Value)
            .HasColumnName("quantity");

        builder.Property(x => x.OrderSide).HasConversion<string>().HasMaxLength(20).HasColumnName("order_side");
        builder.Property(x => x.Status).HasConversion<string>().HasMaxLength(20).HasColumnName("status");

        builder.ComplexProperty(x => x.OrderTerms, t =>
        {
            t.Property(z => z.LimitPrice).HasConversion(p => p!.Value, l => Price.CreateOptional(l).Value)
                .HasColumnName("limit_price");

            t.Property(c => c.TimeInForce).HasConversion<string>().HasMaxLength(20).HasColumnName("time_in_force");
            t.Property(c => c.Type).HasConversion<string>().HasMaxLength(20).HasColumnName("order_type");
        });

        builder.Property(x => x.CancellationReason).HasColumnName("cancellation_reason");
        builder.Property(x => x.CanceledAt).HasColumnName("canceled_at");

        builder.Property(x => x.RejectionReason).HasColumnName("rejection_reason");
        builder.Property(x => x.RejectedAt).HasColumnName("rejected_at");

        builder.Property(x => x.ApprovedAt).HasColumnName("approved_at");
        builder.Property(x => x.FilledAt).HasColumnName("filled_at");
        builder.Property(x => x.ExpiredAt).HasColumnName("expired_at");
        builder.Property(x => x.CreatedAt).HasColumnName("created_at");

        builder.Property<byte[]>("row_version").IsRowVersion();

        builder.HasIndex(x => new { x.UserId, x.CreatedAt, x.Id }).IsDescending().HasDatabaseName("order_user_id_idx");
        builder.HasIndex(x => x.Status)
            .HasFilter("[status] IN (N'PendingNew', N'New', N'PartiallyFilled', N'PendingCancel')")
            .HasDatabaseName("order_status_partial_idx");
    }
}