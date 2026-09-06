using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using VIHouse.Entities.Experiences;

namespace VIHouse.DataAccess.Concrete.EntityFramework.Configurations;

public class ExperienceTranslationConfiguration : IEntityTypeConfiguration<ExperienceTranslation>
{
    public void Configure(EntityTypeBuilder<ExperienceTranslation> builder)
    {
        builder.ToTable("ExperienceTranslations");

        // One row per culture per experience. Enforced in the database rather than only in the
        // service, for the reason SeminarTranslation gives: a double-submitted "add German" would
        // otherwise leave two German copies and the read side picks whichever comes back first.
        builder.HasIndex(t => new { t.ExperienceId, t.Culture }).IsUnique();

        builder.Property(t => t.Culture).HasMaxLength(10).IsRequired();
        builder.Property(t => t.Title).HasMaxLength(200).IsRequired();
        builder.Property(t => t.ShortSummary).HasMaxLength(400);
        builder.Property(t => t.Venue).HasMaxLength(200);
        builder.Property(t => t.AudienceTags).HasMaxLength(300);
        builder.Property(t => t.SeoTitle).HasMaxLength(200);
        builder.Property(t => t.SeoDescription).HasMaxLength(400);
        builder.Property(t => t.CoverImageAlt).HasMaxLength(300);

        // Description is left unbounded, matching the column it overlays.
    }
}
