using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using VIHouse.DataAccess.Identity;
using VIHouse.Entities.Referrals;

namespace VIHouse.DataAccess.Concrete.EntityFramework.Configurations;

public class AmbassadorConfiguration : IEntityTypeConfiguration<Ambassador>
{
    public void Configure(EntityTypeBuilder<Ambassador> builder)
    {
        builder.ToTable("Ambassadors");

        builder.HasIndex(a => a.Code).IsUnique();
        builder.Property(a => a.Code).HasMaxLength(40).IsRequired();
        builder.Property(a => a.Name).HasMaxLength(150).IsRequired();
        builder.Property(a => a.CommissionPercent).HasPrecision(5, 2);

        builder.HasOne<ApplicationUser>().WithMany().HasForeignKey(a => a.UserId).OnDelete(DeleteBehavior.Restrict);

        builder.Property(a => a.InviteEmail).HasMaxLength(256);
        builder.Property(a => a.PreferredCulture).HasMaxLength(10);
        builder.Property(a => a.InviteTokenHash).HasMaxLength(64);
        builder.HasIndex(a => a.InviteTokenHash).IsUnique().HasFilter("[InviteTokenHash] IS NOT NULL");
        builder.Property(a => a.TermsVersion).HasMaxLength(20);
        builder.Property(a => a.TermsCommissionPercent).HasPrecision(5, 2);
        builder.Property(a => a.BillingAddressLine1).HasMaxLength(200);
        builder.Property(a => a.BillingAddressLine2).HasMaxLength(200);
        builder.Property(a => a.BillingCity).HasMaxLength(100);
        builder.Property(a => a.BillingPostalCode).HasMaxLength(20);
        builder.Property(a => a.BillingCountry).HasMaxLength(2);
        builder.Property(a => a.TaxId).HasMaxLength(40);
        builder.Property(a => a.PayoutAccountHolder).HasMaxLength(150);
        builder.Property(a => a.PayoutIban).HasMaxLength(34);
        builder.Property(a => a.PayoutBic).HasMaxLength(11);
        builder.Ignore(a => a.HasPayoutDetails);

        builder.Property(a => a.LegalFirstName).HasMaxLength(100);
        builder.Property(a => a.LegalLastName).HasMaxLength(100);
        builder.Property(a => a.Bio).HasMaxLength(1000);
        builder.Property(a => a.Niche).HasMaxLength(200);
        builder.Property(a => a.PhotoStorageKey).HasMaxLength(300);

        builder.HasMany(a => a.Channels)
            .WithOne()
            .HasForeignKey(c => c.AmbassadorId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}

public class AmbassadorChannelConfiguration : IEntityTypeConfiguration<AmbassadorChannel>
{
    public void Configure(EntityTypeBuilder<AmbassadorChannel> builder)
    {
        builder.ToTable("AmbassadorChannels");
        builder.Property(c => c.Platform).HasConversion<string>().HasMaxLength(20);
        builder.Property(c => c.Url).HasMaxLength(300).IsRequired();
        builder.HasIndex(c => new { c.AmbassadorId, c.SortOrder });
    }
}
