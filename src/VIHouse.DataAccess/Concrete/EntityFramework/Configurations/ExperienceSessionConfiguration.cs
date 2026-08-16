using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using VIHouse.Entities.Experiences;

namespace VIHouse.DataAccess.Concrete.EntityFramework.Configurations;

public class ExperienceSessionConfiguration : IEntityTypeConfiguration<ExperienceSession>
{
    public void Configure(EntityTypeBuilder<ExperienceSession> builder)
    {
        builder.ToTable("ExperienceSessions");
        builder.HasIndex(s => new { s.ProgramDayId, s.SortOrder });
        builder.Property(s => s.Title).HasMaxLength(200).IsRequired();
        builder.Property(s => s.SpeakerName).HasMaxLength(200);
    }
}
