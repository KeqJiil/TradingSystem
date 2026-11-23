using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Order.Infrastructure.Persistence.Configuration;

public class StockConfiguration : IEntityTypeConfiguration<Domain.Aggregates.Stock>
{
    public void Configure(EntityTypeBuilder<Domain.Aggregates.Stock> builder)
    {
        builder.ToTable("stock", "order");

        builder.HasKey(x => x.Id).HasName("pk_stock");
        builder.Property(x => x.Id).HasColumnName("stock_id").ValueGeneratedNever();

        builder.Property(x => x.Name).HasMaxLength(20).IsRequired().HasColumnName("name");
        builder.Property(x => x.IsOpenToTrade).HasColumnName("is_open_to_trade");
        builder.Property(x => x.TradingStart).HasColumnType("time").HasColumnName("trading_start");
        builder.Property(x => x.TradingEnd).HasColumnType("time").HasColumnName("trading_end");

        builder.Property(x => x.NameVersion).HasDefaultValue(0L).HasColumnName("name_version");
        builder.Property(x => x.StatusVersion).HasDefaultValue(0L).HasColumnName("status_version");
        builder.Property(x => x.TradingTimeVersion).HasDefaultValue(0L).HasColumnName("trading_time_version");
    }
}
