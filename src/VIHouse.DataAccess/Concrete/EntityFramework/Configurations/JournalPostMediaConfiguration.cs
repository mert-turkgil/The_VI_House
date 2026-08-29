using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using VIHouse.Entities.Journal;

namespace VIHouse.DataAccess.Concrete.EntityFramework.Configurations;

public class JournalPostMediaConfiguration : IEntityTypeConfiguration<JournalPostMedia>
{
    public void Configure(EntityTypeBuilder<JournalPostMedia> builder)
    {
        builder.ToTable("JournalPostMedia");

        builder.HasIndex(m => new { m.JournalPostId, m.SortOrder });

        builder.Property(m => m.StorageKey).HasMaxLength(500).IsRequired();
        builder.Property(m => m.Title).HasMaxLength(200);
        builder.Property(m => m.ContentType).HasMaxLength(100).IsRequired();
        builder.Property(m => m.OriginalFileName).HasMaxLength(255);
    }
}
