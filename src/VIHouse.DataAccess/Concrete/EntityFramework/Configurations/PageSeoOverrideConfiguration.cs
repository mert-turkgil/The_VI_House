using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using VIHouse.Entities.Settings;

namespace VIHouse.DataAccess.Concrete.EntityFramework.Configurations;

public class PageSeoOverrideConfiguration : IEntityTypeConfiguration<PageSeoOverride>
{
    public void Configure(EntityTypeBuilder<PageSeoOverride> builder)
    {
        builder.ToTable("PageSeoOverrides");
        builder.Property(p => p.PageKey).HasMaxLength(40).IsRequired();
        builder.Property(p => p.Culture).HasMaxLength(10).IsRequired();
        builder.Property(p => p.Title).HasMaxLength(120);
        builder.Property(p => p.Description).HasMaxLength(320);
        builder.HasIndex(p => new { p.SiteSettingId, p.PageKey, p.Culture }).IsUnique();
        builder.HasOne<SiteSetting>().WithMany(s => s.PageSeoOverrides).HasForeignKey(p => p.SiteSettingId).OnDelete(DeleteBehavior.Cascade);
    }
}
