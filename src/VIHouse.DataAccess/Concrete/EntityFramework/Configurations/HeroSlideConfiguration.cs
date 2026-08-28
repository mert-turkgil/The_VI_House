using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using VIHouse.Entities.Content;

namespace VIHouse.DataAccess.Concrete.EntityFramework.Configurations;

public class HeroSlideConfiguration : IEntityTypeConfiguration<HeroSlide>
{
    public void Configure(EntityTypeBuilder<HeroSlide> builder)
    {
        builder.ToTable("HeroSlides");

        // The homepage's only query: active slides in order. Covering both columns keeps it an
        // index seek even once retired slides outnumber live ones.
        builder.HasIndex(s => new { s.IsActive, s.SortOrder });

        builder.Property(s => s.ImageUrl).HasMaxLength(1000);
        builder.Property(s => s.ImageStorageKey).HasMaxLength(1000);
        builder.Property(s => s.PrimaryCtaUrl).HasMaxLength(500);
        builder.Property(s => s.SecondaryCtaUrl).HasMaxLength(500);

        // Deleting a slide takes its copy with it. There is nothing meaningful about a translation
        // whose slide is gone, and leaving orphans behind would slowly fill the table with rows no
        // screen can reach.
        builder.HasMany(s => s.Translations)
            .WithOne()
            .HasForeignKey(t => t.HeroSlideId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
