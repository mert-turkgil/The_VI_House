using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using VIHouse.Entities.Content;

namespace VIHouse.DataAccess.Concrete.EntityFramework.Configurations;

public class ContentPageConfiguration : IEntityTypeConfiguration<ContentPage>
{
    public void Configure(EntityTypeBuilder<ContentPage> builder)
    {
        builder.ToTable("ContentPages");
        builder.HasIndex(p => p.Slug).IsUnique();
        builder.Property(p => p.Slug).HasMaxLength(200).IsRequired();
        builder.Property(p => p.Title).HasMaxLength(200).IsRequired();

        builder.HasMany(p => p.Blocks)
            .WithOne()
            .HasForeignKey(b => b.PageId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
