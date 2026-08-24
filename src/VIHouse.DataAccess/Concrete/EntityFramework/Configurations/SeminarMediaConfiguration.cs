using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using VIHouse.Entities.Seminars;

namespace VIHouse.DataAccess.Concrete.EntityFramework.Configurations;

public class SeminarMediaConfiguration : IEntityTypeConfiguration<SeminarMedia>
{
    public void Configure(EntityTypeBuilder<SeminarMedia> builder)
    {
        builder.ToTable("SeminarMedia");

        builder.HasIndex(m => new { m.SeminarId, m.SortOrder });

        builder.Property(m => m.StorageKey).HasMaxLength(500).IsRequired();
        builder.Property(m => m.Title).HasMaxLength(200);
        builder.Property(m => m.ContentType).HasMaxLength(100).IsRequired();
        builder.Property(m => m.OriginalFileName).HasMaxLength(255);
    }
}
