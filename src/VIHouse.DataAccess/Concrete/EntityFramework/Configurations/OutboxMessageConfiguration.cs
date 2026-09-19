using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using VIHouse.Entities.Commerce;

namespace VIHouse.DataAccess.Concrete.EntityFramework.Configurations;

public class OutboxMessageConfiguration : IEntityTypeConfiguration<OutboxMessage>
{
    public void Configure(EntityTypeBuilder<OutboxMessage> builder)
    {
        builder.ToTable("OutboxMessages");
        builder.Property(m => m.Kind).HasConversion<string>().HasMaxLength(20);
        builder.Property(m => m.DedupeKey).HasMaxLength(300).IsRequired();
        builder.Property(m => m.Payload).IsRequired();
        builder.Property(m => m.RelatedEntityType).HasMaxLength(100);
        builder.Property(m => m.LastError).HasMaxLength(2000);

        // The one-of-a-kind guarantee: the same effect for the same outcome cannot be queued twice.
        builder.HasIndex(m => m.DedupeKey).IsUnique();
        // What the processor polls: due, not yet done, oldest first.
        builder.HasIndex(m => new { m.ProcessedAt, m.NextAttemptAt });
        builder.HasIndex(m => new { m.RelatedEntityType, m.RelatedEntityId });
    }
}
