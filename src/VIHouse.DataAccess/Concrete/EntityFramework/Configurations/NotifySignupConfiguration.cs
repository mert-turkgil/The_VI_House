using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using VIHouse.Entities.Marketing;

namespace VIHouse.DataAccess.Concrete.EntityFramework.Configurations;

public class NotifySignupConfiguration : IEntityTypeConfiguration<NotifySignup>
{
    public void Configure(EntityTypeBuilder<NotifySignup> builder)
    {
        builder.ToTable("NotifySignups");

        // 320 is the maximum length of an email address (64 local + @ + 255 domain), the same figure
        // WaitlistEntry and Application use.
        builder.Property(n => n.Email).HasMaxLength(320).IsRequired();

        // One row per address, globally — unlike WaitlistEntry's (ExperienceId, Email), because this
        // list is about the site rather than about an event. Without it, ten refreshes of a public
        // form are ten rows and the launch announcement arrives ten times. The service reads the
        // existing row back rather than reporting a failure: a repeat sign-up is not an error, it is
        // "you are already on the list", which is both true and what they wanted to hear.
        builder.HasIndex(n => n.Email).IsUnique();

        builder.Property(n => n.Culture).HasMaxLength(10).IsRequired();
        builder.Property(n => n.Source).HasMaxLength(50);

        // Backs the admin list's ORDER BY. This table only ever grows and is never pruned, so the
        // newest-first page needs to stay cheap however long the curtain is up.
        builder.HasIndex(n => n.CreatedAt);
    }
}
