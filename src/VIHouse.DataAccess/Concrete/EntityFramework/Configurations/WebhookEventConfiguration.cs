using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using VIHouse.Entities.Commerce;

namespace VIHouse.DataAccess.Concrete.EntityFramework.Configurations;

public class WebhookEventConfiguration : IEntityTypeConfiguration<WebhookEvent>
{
    public void Configure(EntityTypeBuilder<WebhookEvent> builder)
    {
        builder.ToTable("WebhookEvents");
        builder.HasKey(e => e.EventId);
        builder.Property(e => e.EventId).HasMaxLength(255);
        builder.Property(e => e.Type).HasMaxLength(100).IsRequired();
        builder.Property(e => e.ObjectId).HasMaxLength(255);
        builder.Property(e => e.LastError).HasMaxLength(2000);
        builder.Property(e => e.PayloadHash).HasMaxLength(64).IsRequired();
        builder.Property(e => e.Status).HasConversion<string>().HasMaxLength(20);

        // The admin screen lists by status, newest first; support looks events up by the Stripe
        // object they concern.
        builder.HasIndex(e => new { e.Status, e.ReceivedAt });
        builder.HasIndex(e => e.ObjectId);
    }
}
