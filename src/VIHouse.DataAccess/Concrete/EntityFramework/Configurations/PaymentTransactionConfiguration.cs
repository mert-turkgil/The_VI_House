using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using VIHouse.DataAccess.Identity;
using VIHouse.Entities.Commerce;

namespace VIHouse.DataAccess.Concrete.EntityFramework.Configurations;

public class PaymentTransactionConfiguration : IEntityTypeConfiguration<PaymentTransaction>
{
    public void Configure(EntityTypeBuilder<PaymentTransaction> builder)
    {
        builder.ToTable("PaymentTransactions");

        builder.Property(t => t.Kind).HasConversion<string>().HasMaxLength(30);
        builder.Property(t => t.Status).HasConversion<string>().HasMaxLength(30);
        builder.Property(t => t.RelatedEntityType).HasMaxLength(50).IsRequired();
        builder.Property(t => t.Currency).HasMaxLength(3).IsRequired();
        builder.Property(t => t.ProviderCustomerId).HasMaxLength(100);
        builder.Property(t => t.ProviderSessionId).HasMaxLength(200);
        builder.Property(t => t.ProviderPaymentIntentId).HasMaxLength(100);
        builder.Property(t => t.ProviderChargeId).HasMaxLength(100);
        builder.Property(t => t.ProviderSubscriptionId).HasMaxLength(100);
        builder.Property(t => t.ProviderInvoiceId).HasMaxLength(100);
        builder.Property(t => t.ProviderReceiptUrl).HasMaxLength(500);
        builder.Property(t => t.FailureCode).HasMaxLength(100);
        builder.Property(t => t.FailureMessage).HasMaxLength(1000);
        builder.Property(t => t.LastEventId).HasMaxLength(255);
        builder.Property(t => t.RowVersion).IsRowVersion();

        // A provider object pays for one transaction, ever. Filtered because most rows have only
        // some of these ids, and SQL Server treats every NULL in a unique index as equal.
        builder.HasIndex(t => t.ProviderSessionId).IsUnique().HasFilter("[ProviderSessionId] IS NOT NULL");
        builder.HasIndex(t => t.ProviderPaymentIntentId).IsUnique().HasFilter("[ProviderPaymentIntentId] IS NOT NULL");
        builder.HasIndex(t => t.ProviderInvoiceId).IsUnique().HasFilter("[ProviderInvoiceId] IS NOT NULL");
        builder.HasIndex(t => t.ProviderChargeId).HasFilter("[ProviderChargeId] IS NOT NULL");
        // Subscriptions have many transactions (one per renewal); the index is for lookup only.
        builder.HasIndex(t => t.ProviderSubscriptionId).HasFilter("[ProviderSubscriptionId] IS NOT NULL");
        builder.HasIndex(t => new { t.RelatedEntityType, t.RelatedEntityId });
        builder.HasIndex(t => new { t.Kind, t.Status });
        builder.HasIndex(t => new { t.UserId, t.CreatedAt });

        builder.HasOne<ApplicationUser>().WithMany().HasForeignKey(t => t.UserId).OnDelete(DeleteBehavior.Restrict);
    }
}
