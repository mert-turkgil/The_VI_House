using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using VIHouse.Entities.Communication;

namespace VIHouse.DataAccess.Concrete.EntityFramework.Configurations;

public class SmsLogConfiguration : IEntityTypeConfiguration<SmsLog>
{
    public void Configure(EntityTypeBuilder<SmsLog> builder)
    {
        builder.ToTable("SmsLogs");
        builder.HasIndex(e => e.RecipientPhone);
        builder.HasIndex(e => new { e.RelatedEntityType, e.RelatedEntityId });
        builder.Property(e => e.TemplateKey).HasMaxLength(100).IsRequired();
        builder.Property(e => e.RecipientPhone).HasMaxLength(40).IsRequired();
    }
}
