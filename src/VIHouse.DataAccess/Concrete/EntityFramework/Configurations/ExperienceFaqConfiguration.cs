using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using VIHouse.Entities.Experiences;

namespace VIHouse.DataAccess.Concrete.EntityFramework.Configurations;

public class ExperienceFaqConfiguration : IEntityTypeConfiguration<ExperienceFaq>
{
    public void Configure(EntityTypeBuilder<ExperienceFaq> builder)
    {
        builder.ToTable("ExperienceFaqs");
        builder.HasIndex(f => new { f.ExperienceId, f.SortOrder });
        builder.Property(f => f.Question).HasMaxLength(300).IsRequired();
    }
}
