using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using VIHouse.Entities.Experiences;

namespace VIHouse.DataAccess.Concrete.EntityFramework.Configurations;

public class ExperienceProgramDayConfiguration : IEntityTypeConfiguration<ExperienceProgramDay>
{
    public void Configure(EntityTypeBuilder<ExperienceProgramDay> builder)
    {
        builder.ToTable("ExperienceProgramDays");
        builder.HasIndex(d => new { d.ExperienceId, d.SortOrder });
        builder.Property(d => d.Title).HasMaxLength(200).IsRequired();

        builder.HasMany(d => d.Sessions)
            .WithOne()
            .HasForeignKey(s => s.ProgramDayId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
