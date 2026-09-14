using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using VIHouse.Entities.Membership;

namespace VIHouse.DataAccess.Concrete.EntityFramework.Configurations;

public class PendingJoinConfiguration : IEntityTypeConfiguration<PendingJoin>
{
    public void Configure(EntityTypeBuilder<PendingJoin> builder)
    {
        builder.ToTable("PendingJoins");

        builder.Property(p => p.Code).HasMaxLength(32).IsRequired();
        builder.HasIndex(p => p.Code).IsUnique();

        builder.Property(p => p.Email).HasMaxLength(320).IsRequired();
        builder.Property(p => p.EmailNormalized).HasMaxLength(320).IsRequired();
        // Not unique — the same address can leave several rows behind (abandon, retry, change
        // plan). The service picks the newest open one; the rest get Superseded.
        builder.HasIndex(p => p.EmailNormalized);

        builder.Property(p => p.FirstName).HasMaxLength(100).IsRequired();
        builder.Property(p => p.LastName).HasMaxLength(100).IsRequired();
        builder.Property(p => p.Country).HasMaxLength(2).IsRequired();
        builder.Property(p => p.City).HasMaxLength(100);
        // Same widths as ProfileConfiguration, because these become the profile on payment.
        builder.Property(p => p.JobTitle).HasMaxLength(200);
        builder.Property(p => p.AddressLine1).HasMaxLength(200);
        builder.Property(p => p.AddressLine2).HasMaxLength(200);
        builder.Property(p => p.PostalCode).HasMaxLength(20);
        builder.Property(p => p.About).HasMaxLength(2000);
        builder.Property(p => p.Expectations).HasMaxLength(2000);
        builder.Property(p => p.EarningsBand).HasMaxLength(20);
        builder.Property(p => p.ReferralCode).HasMaxLength(40);
        builder.Property(p => p.IpAddress).HasMaxLength(64);

        builder.Property(p => p.ProviderSessionId).HasMaxLength(200);
        // Filtered, and the filter is load-bearing: SQL Server treats NULLs as equal in a unique
        // index, and a row whose provider call failed legitimately sits at NULL — several of them
        // can at once.
        builder.HasIndex(p => p.ProviderSessionId)
            .IsUnique()
            .HasFilter("[ProviderSessionId] IS NOT NULL");

        builder.Property(p => p.CheckoutUrl).HasMaxLength(2048);

        builder.HasOne<MembershipPlan>().WithMany().HasForeignKey(p => p.PlanId).OnDelete(DeleteBehavior.Restrict);
    }
}
