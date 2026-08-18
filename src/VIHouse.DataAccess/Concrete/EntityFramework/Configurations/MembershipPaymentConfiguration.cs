using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using VIHouse.DataAccess.Identity;
using VIHouse.Entities.Membership;

namespace VIHouse.DataAccess.Concrete.EntityFramework.Configurations;

public class MembershipPaymentConfiguration : IEntityTypeConfiguration<MembershipPayment>
{
    public void Configure(EntityTypeBuilder<MembershipPayment> builder)
    {
        builder.ToTable("MembershipPayments");

        builder.HasIndex(p => p.ProviderReference).IsUnique();
        builder.Property(p => p.ProviderReference).HasMaxLength(200).IsRequired();
        builder.Property(p => p.Currency).HasMaxLength(3).IsRequired();

        builder.HasOne<ApplicationUser>().WithMany().HasForeignKey(p => p.UserId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<MembershipPlan>().WithMany().HasForeignKey(p => p.PlanId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<Membership>().WithMany().HasForeignKey(p => p.MembershipId).OnDelete(DeleteBehavior.Restrict);
    }
}
