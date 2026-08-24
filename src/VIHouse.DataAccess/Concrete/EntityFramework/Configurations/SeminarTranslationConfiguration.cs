using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using VIHouse.Entities.Seminars;

namespace VIHouse.DataAccess.Concrete.EntityFramework.Configurations;

public class SeminarTranslationConfiguration : IEntityTypeConfiguration<SeminarTranslation>
{
    public void Configure(EntityTypeBuilder<SeminarTranslation> builder)
    {
        builder.ToTable("SeminarTranslations");

        // One row per culture per seminar. Enforced in the database rather than only in the service
        // because a double-submitted "add German" would otherwise leave two German bodies, and the
        // read side picks whichever the query happens to return first.
        builder.HasIndex(t => new { t.SeminarId, t.Culture }).IsUnique();

        builder.Property(t => t.Culture).HasMaxLength(10).IsRequired();
        builder.Property(t => t.Title).HasMaxLength(200).IsRequired();
        builder.Property(t => t.Summary).HasMaxLength(600);
        builder.Property(t => t.SeoTitle).HasMaxLength(200);
        builder.Property(t => t.SeoDescription).HasMaxLength(400);

        // Left unbounded (nvarchar(max)): this is a full article of editor HTML, and a length cap
        // here would surface as a truncated body rather than a validation error.
        builder.Property(t => t.BodyHtml).IsRequired();
    }
}
