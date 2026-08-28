using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using VIHouse.Entities.Content;

namespace VIHouse.DataAccess.Concrete.EntityFramework.Configurations;

public class HeroSlideTranslationConfiguration : IEntityTypeConfiguration<HeroSlideTranslation>
{
    public void Configure(EntityTypeBuilder<HeroSlideTranslation> builder)
    {
        builder.ToTable("HeroSlideTranslations");

        // One row per culture per slide, enforced here for the same reason SeminarTranslations is:
        // a double-submitted "add German" would otherwise leave two German headings and the read
        // side would show whichever the query returned first.
        builder.HasIndex(t => new { t.HeroSlideId, t.Culture }).IsUnique();

        builder.Property(t => t.Culture).HasMaxLength(10).IsRequired();
        builder.Property(t => t.Eyebrow).HasMaxLength(120);
        builder.Property(t => t.Heading).HasMaxLength(200).IsRequired();
        builder.Property(t => t.Subheading).HasMaxLength(600);
        builder.Property(t => t.PrimaryCtaLabel).HasMaxLength(60);
        builder.Property(t => t.SecondaryCtaLabel).HasMaxLength(60);
        builder.Property(t => t.ImageAlt).HasMaxLength(300);
    }
}
