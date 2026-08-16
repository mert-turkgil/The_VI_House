using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using VIHouse.Entities.Content;

namespace VIHouse.DataAccess.Concrete.EntityFramework.Configurations;

public class ContentBlockConfiguration : IEntityTypeConfiguration<ContentBlock>
{
    public void Configure(EntityTypeBuilder<ContentBlock> builder)
    {
        builder.ToTable("ContentBlocks");
        builder.HasIndex(b => new { b.PageId, b.SectionKey, b.SortOrder });
        builder.Property(b => b.SectionKey).HasMaxLength(100).IsRequired();
        // ExtraJson/BodyText left unbounded (nvarchar(max)) — rich/flexible CMS payloads.
    }
}
