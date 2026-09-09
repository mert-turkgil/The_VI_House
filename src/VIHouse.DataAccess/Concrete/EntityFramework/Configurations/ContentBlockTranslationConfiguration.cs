using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using VIHouse.Entities.Content;

namespace VIHouse.DataAccess.Concrete.EntityFramework.Configurations;

public class ContentBlockTranslationConfiguration : IEntityTypeConfiguration<ContentBlockTranslation>
{
    public void Configure(EntityTypeBuilder<ContentBlockTranslation> builder)
    {
        builder.ToTable("ContentBlockTranslations");

        // One row per culture per block, enforced in the database rather than only in the service —
        // the same guard every other translation table in this schema carries, and for the same
        // reason: a double-submitted "add Turkish" would otherwise leave two Turkish rows and the
        // read side would show whichever the query happened to return first.
        builder.HasIndex(t => new { t.ContentBlockId, t.Culture }).IsUnique();

        builder.Property(t => t.Culture).HasMaxLength(10).IsRequired();
        builder.Property(t => t.Heading).HasMaxLength(300);
        builder.Property(t => t.Subheading).HasMaxLength(300);
        builder.Property(t => t.CtaLabel).HasMaxLength(120);

        // BodyText and ExtraJson stay unbounded, matching the columns they overlay: one is a
        // paragraph of prose, the other a JSON array of testimonials. A cap on either would surface
        // as silently truncated content rather than as a validation message.

        builder.HasOne<ContentBlock>()
            .WithMany(b => b.Translations)
            .HasForeignKey(t => t.ContentBlockId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
