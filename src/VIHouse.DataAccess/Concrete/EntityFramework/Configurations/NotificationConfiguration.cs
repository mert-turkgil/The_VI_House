using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using VIHouse.DataAccess.Identity;
using VIHouse.Entities.Notifications;

namespace VIHouse.DataAccess.Concrete.EntityFramework.Configurations;

public class NotificationConfiguration : IEntityTypeConfiguration<Notification>
{
    public void Configure(EntityTypeBuilder<Notification> builder)
    {
        builder.ToTable("Notifications");

        builder.Property(n => n.Title).HasMaxLength(200).IsRequired();
        builder.Property(n => n.Body).HasMaxLength(1000).IsRequired();
        builder.Property(n => n.Link).HasMaxLength(300);

        builder.HasIndex(n => new { n.UserId, n.IsRead });

        builder.HasOne<ApplicationUser>().WithMany().HasForeignKey(n => n.UserId).OnDelete(DeleteBehavior.Restrict);
    }
}
