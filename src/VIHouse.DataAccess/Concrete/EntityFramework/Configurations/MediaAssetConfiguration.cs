using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using VIHouse.Entities.Content;

namespace VIHouse.DataAccess.Concrete.EntityFramework.Configurations;

public class MediaAssetConfiguration : IEntityTypeConfiguration<MediaAsset>
{
    public void Configure(EntityTypeBuilder<MediaAsset> builder)
    {
        builder.ToTable("MediaAssets");

        // The panel lists newest first and nothing else queries this table.
        builder.HasIndex(a => a.CreatedAt);

        builder.Property(a => a.StorageKey).HasMaxLength(500).IsRequired();
        builder.Property(a => a.Title).HasMaxLength(200);
        builder.Property(a => a.ContentType).HasMaxLength(100).IsRequired();
        builder.Property(a => a.OriginalFileName).HasMaxLength(255);
    }
}
