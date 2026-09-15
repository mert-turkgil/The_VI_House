using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using VIHouse.DataAccess.Identity;
using VIHouse.Entities.Users;

namespace VIHouse.DataAccess.Concrete.EntityFramework.Configurations;

public class SignInRecordConfiguration : IEntityTypeConfiguration<SignInRecord>
{
    public void Configure(EntityTypeBuilder<SignInRecord> builder)
    {
        builder.ToTable("SignInRecords");
        builder.Property(r => r.IpAddress).HasMaxLength(64);
        builder.Property(r => r.UserAgent).HasMaxLength(300);
        builder.HasIndex(r => new { r.UserId, r.At });
        builder.HasOne<ApplicationUser>().WithMany().HasForeignKey(r => r.UserId).OnDelete(DeleteBehavior.Cascade);
    }
}
