using Messaging.EFCore.Inbox;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Messaging.EFCore.Persistence;

internal class InboxConfiguration : IEntityTypeConfiguration<InboxEntity>
{
    public void Configure(EntityTypeBuilder<InboxEntity> builder)
    {
        builder.ToTable("messaging.inbox");

        builder.HasKey(x => new { x.ConsumerGroup, x.MessageId }).HasName("pk_inbox");

        builder.Property(x => x.ConsumerGroup)
            .HasColumnName("consumer_group")
            .HasColumnType("VARCHAR(200)")
            .IsRequired();

        builder.Property(x => x.MessageId)
            .HasColumnName("message_id")
            .HasColumnType("VARCHAR(100)")
            .IsRequired();

        builder.Property<DateTimeOffset>(x => x.CreatedAt).HasDefaultValueSql("SYSDATETIMEOFFSET()")
            .HasColumnName("created_at").IsRequired();

        builder.HasIndex(x => x.CreatedAt).HasDatabaseName("idx_inbox_created_at");
    }
}

public static class RegisterInboxConfiguration
{
    public static ModelBuilder RegisterInbox(this ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfiguration(new InboxConfiguration());
        return modelBuilder;
    }
}