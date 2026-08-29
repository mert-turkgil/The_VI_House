using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using VIHouse.Entities.Journal;

namespace VIHouse.DataAccess.Concrete.EntityFramework.Configurations;

public class JournalPostTranslationConfiguration : IEntityTypeConfiguration<JournalPostTranslation>
{
    public void Configure(EntityTypeBuilder<JournalPostTranslation> builder)
    {
        builder.ToTable("JournalPostTranslations");

        // One row per culture per post, enforced in the database rather than only in the service:
        // a double-submitted "add German" would otherwise leave two German articles and the read
        // side would show whichever the query returned first.
        builder.HasIndex(t => new { t.JournalPostId, t.Culture }).IsUnique();

        builder.Property(t => t.Culture).HasMaxLength(10).IsRequired();
        builder.Property(t => t.Title).HasMaxLength(200).IsRequired();
        builder.Property(t => t.Excerpt).HasMaxLength(500);

        // Left unbounded (nvarchar(max)): this is a full article of editor HTML, and a length cap
        // here would surface as a truncated body rather than a validation error.
        builder.Property(t => t.Body).IsRequired();
    }
}
