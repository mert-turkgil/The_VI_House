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

        builder.Property(p => p.Slug).HasMaxLength(200).IsRequired();
        builder.Property(p => p.AuthorName).HasMaxLength(150);
        builder.Property(p => p.CoverImageUrl).HasMaxLength(1000);
        builder.Property(p => p.CoverImageAlt).HasMaxLength(300);

        // Deleting a post takes its copy and its files with it. The files themselves are removed by
        // JournalService after the delete commits — the database cascade only clears the rows that
        // point at them.
        builder.HasMany(p => p.Translations)
            .WithOne()
            .HasForeignKey(t => t.JournalPostId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasMany(p => p.Media)
            .WithOne()
            .HasForeignKey(m => m.JournalPostId)
            .OnDelete(DeleteBehavior.Cascade);

        // CoverMediaId is deliberately left as a plain nullable Guid with no FK constraint, exactly
        // as Seminar.CoverMediaId is: a real one would be circular (JournalPost -> JournalPostMedia
        // -> JournalPost) and would fight the cascade delete above. JournalService clears the
        // pointer when the chosen image is removed, and the read side treats a dangling id as "no
        // cover" rather than an error.
    }
}
