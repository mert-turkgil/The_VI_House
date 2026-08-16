using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using VIHouse.Entities.Compliance;

namespace VIHouse.DataAccess.Concrete.EntityFramework.Configurations;

public class ConsentRecordConfiguration : IEntityTypeConfiguration<ConsentRecord>
{
    public void Configure(EntityTypeBuilder<ConsentRecord> builder)
    {
        builder.ToTable("ConsentRecords");
        builder.HasIndex(c => c.UserId);
        builder.Property(c => c.IpAddress).HasMaxLength(45);
    }
}
