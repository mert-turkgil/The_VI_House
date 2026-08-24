using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using VIHouse.DataAccess.Identity;
using VIHouse.Entities.Seminars;

namespace VIHouse.DataAccess.Concrete.EntityFramework.Configurations;

public class SeminarEnrollmentConfiguration : IEntityTypeConfiguration<SeminarEnrollment>
{
    public void Configure(EntityTypeBuilder<SeminarEnrollment> builder)
    {
        builder.ToTable("SeminarEnrollments");

        // The real guard against a double enrolment: two tabs, or a webhook arriving while the
        // browser is still on the success page, both end up trying to insert the same pair.
        builder.HasIndex(e => new { e.SeminarId, e.UserId }).IsUnique();

        // Filtered rather than plain-unique: most rows have no provider reference at all (free and
        // membership-covered enrolments never touch Stripe), and SQL Server treats every NULL in a
        // unique index as equal to every other one.
        builder.HasIndex(e => e.ProviderReference)
            .IsUnique()
            .HasFilter("[ProviderReference] IS NOT NULL");

        builder.Property(e => e.ProviderReference).HasMaxLength(200);
        builder.Property(e => e.Currency).HasMaxLength(3).IsRequired();

        // Restrict, not Cascade, on both sides: an enrolment is a financial record. Deleting a
        // seminar someone paid for should fail loudly rather than quietly erase the evidence —
        // SeminarService archives instead, and refuses the delete outright once anyone has enrolled.
        builder.HasOne<Seminar>().WithMany().HasForeignKey(e => e.SeminarId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<ApplicationUser>().WithMany().HasForeignKey(e => e.UserId).OnDelete(DeleteBehavior.Restrict);
    }
}
