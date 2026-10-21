using Messaging.EFCore.Outbox;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Messaging.EFCore.Persistence;

internal class OutboxConfiguration : IEntityTypeConfiguration<OutboxEntity>
{
    public void Configure(EntityTypeBuilder<OutboxEntity> builder)
    {
        builder.ToTable("outbox", "messaging");

        builder.HasKey(x => x.Id).HasName("pk_outbox");

        builder.Property<long>(x => x.Id).HasColumnName("id").UseOrderedSequence("outbox_id_seq").IsRequired();
        builder.Property<string>(x => x.MessageId).HasColumnName("message_id").HasColumnType("VARCHAR(100)")
            .IsRequired();
        builder.Property<string>(x => x.MessageType).HasColumnName("message_type").HasColumnType("VARCHAR(200)")
            .IsRequired();
        builder.Property<string>(x => x.Topic).HasColumnName("topic").HasColumnType("VARCHAR(255)").IsRequired();
        builder.Property<string>(x => x.MessageKey).HasColumnName("message_key").HasColumnType("VARCHAR(200)")
            .IsRequired();
        builder.Property<byte[]>(x => x.Payload).HasColumnName("payload").HasColumnType("VARBINARY(MAX)").IsRequired();
        builder.Property<string>(x => x.Headers).HasColumnName("headers").HasColumnType("NVARCHAR(MAX)").IsRequired();
        builder.Property<int>(x => x.Attempts).HasColumnName("attempts").IsRequired();
        builder.Property(x => x.Status).HasConversion<string>().HasColumnName("status").HasColumnType("VARCHAR(20)")
            .IsRequired();
        builder.Property<DateTimeOffset>(x => x.CreatedAt).HasDefaultValueSql("SYSDATETIMEOFFSET()")
            .HasColumnName("created_at").IsRequired();
        builder.Property<DateTimeOffset?>(x => x.ProcessedAt).HasColumnName("processed_at");

        builder.HasIndex(x => x.Id).IncludeProperties(x => x.ProcessedAt)
            .HasFilter("[status] IN ('Pending', 'Processing')").HasDatabaseName("idx_outbox_pending");
        builder.HasIndex(x => x.CreatedAt).HasFilter("[status] = 'Completed'").HasDatabaseName("idx_outbox_completed");
    }
}

public static class RegisterOutboxConfiguration
{
    public static ModelBuilder RegisterOutbox(this ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfiguration(new OutboxConfiguration());
        return modelBuilder;
    }
}