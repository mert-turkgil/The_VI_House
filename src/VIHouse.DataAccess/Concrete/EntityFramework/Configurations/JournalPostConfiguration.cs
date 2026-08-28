using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using VIHouse.Entities.Journal;

namespace VIHouse.DataAccess.Concrete.EntityFramework.Configurations;

public class JournalPostConfiguration : IEntityTypeConfiguration<JournalPost>
{
    public void Configure(EntityTypeBuilder<JournalPost> builder)
    {
        builder.ToTable("JournalPosts");

        builder.HasIndex(p => p.Slug).IsUnique();
        builder.HasIndex(p => new { p.Status, p.PublishedAt });

        builder.Property(p => p.Title).HasMaxLength(200).IsRequired();
        builder.Property(p => p.Slug).HasMaxLength(200).IsRequired();
        builder.Property(p => p.Excerpt).HasMaxLength(500);
        builder.Property(p => p.AuthorName).HasMaxLength(150);
        builder.Property(p => p.CoverImageAlt).HasMaxLength(300);
    }
}
