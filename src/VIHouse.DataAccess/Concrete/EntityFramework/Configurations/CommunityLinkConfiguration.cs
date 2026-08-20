using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using VIHouse.Entities.Community;

namespace VIHouse.DataAccess.Concrete.EntityFramework.Configurations;

public class CommunityLinkConfiguration : IEntityTypeConfiguration<CommunityLink>
{
    public void Configure(EntityTypeBuilder<CommunityLink> builder)
    {
        builder.ToTable("CommunityLinks");

        builder.HasIndex(l => new { l.IsActive, l.SortOrder });

        builder.Property(l => l.Label).HasMaxLength(120).IsRequired();
        builder.Property(l => l.Description).HasMaxLength(400);
        builder.Property(l => l.Url).HasMaxLength(500).IsRequired();
    }
}
