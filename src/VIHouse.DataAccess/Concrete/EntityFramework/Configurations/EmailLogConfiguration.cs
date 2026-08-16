using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using VIHouse.Entities.Communication;

namespace VIHouse.DataAccess.Concrete.EntityFramework.Configurations;

public class EmailLogConfiguration : IEntityTypeConfiguration<EmailLog>
{
    public void Configure(EntityTypeBuilder<EmailLog> builder)
    {
        builder.ToTable("EmailLogs");
        builder.HasIndex(e => e.RecipientEmail);
        builder.HasIndex(e => new { e.RelatedEntityType, e.RelatedEntityId });
        builder.Property(e => e.TemplateKey).HasMaxLength(100).IsRequired();
        builder.Property(e => e.RecipientEmail).HasMaxLength(320).IsRequired();
        builder.Property(e => e.Subject).HasMaxLength(300).IsRequired();
    }
}
