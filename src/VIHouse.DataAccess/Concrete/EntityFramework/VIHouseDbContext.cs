using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using VIHouse.DataAccess.Identity;
using VIHouse.Entities.Applications;
using VIHouse.Entities.Audit;
using VIHouse.Entities.Commerce;
using VIHouse.Entities.Communication;
using VIHouse.Entities.Community;
using VIHouse.Entities.Compliance;
using VIHouse.Entities.Content;
using VIHouse.Entities.Experiences;
using VIHouse.Entities.Journal;
using VIHouse.Entities.Membership;
using VIHouse.Entities.Notifications;
using VIHouse.Entities.Referrals;
using VIHouse.Entities.Seminars;
using VIHouse.Entities.Users;

namespace VIHouse.DataAccess.Concrete.EntityFramework;

/// <summary>
/// Extends IdentityDbContext so ASP.NET Core Identity's AspNetUsers/AspNetRoles/etc tables live
/// alongside the domain schema in one database. Entity configuration is Fluent-API-only, via
/// IEntityTypeConfiguration&lt;T&gt; classes under Configurations/, applied in bulk below — keeps
/// persistence concerns (indexes, constraints, precision) out of VIHouse.Entities entirely.
/// </summary>
public class VIHouseDbContext(DbContextOptions<VIHouseDbContext> options)
    : IdentityDbContext<ApplicationUser, ApplicationRole, Guid>(options)
{
    public DbSet<Profile> Profiles => Set<Profile>();

    public DbSet<Experience> Experiences => Set<Experience>();
    public DbSet<TicketType> TicketTypes => Set<TicketType>();
    public DbSet<ExperienceProgramDay> ExperienceProgramDays => Set<ExperienceProgramDay>();
    public DbSet<ExperienceSession> ExperienceSessions => Set<ExperienceSession>();
    public DbSet<ExperienceInclusion> ExperienceInclusions => Set<ExperienceInclusion>();
    public DbSet<ExperienceFaq> ExperienceFaqs => Set<ExperienceFaq>();
    public DbSet<ExperienceImage> ExperienceImages => Set<ExperienceImage>();

    public DbSet<Application> Applications => Set<Application>();
    public DbSet<ApplicationTag> ApplicationTags => Set<ApplicationTag>();

    public DbSet<Booking> Bookings => Set<Booking>();
    public DbSet<Payment> Payments => Set<Payment>();
    public DbSet<Invitation> Invitations => Set<Invitation>();
    public DbSet<PromoCode> PromoCodes => Set<PromoCode>();
    public DbSet<TicketHold> TicketHolds => Set<TicketHold>();
    public DbSet<WaitlistEntry> WaitlistEntries => Set<WaitlistEntry>();
    public DbSet<ProcessedWebhookEvent> ProcessedWebhookEvents => Set<ProcessedWebhookEvent>();

    public DbSet<ContentPage> ContentPages => Set<ContentPage>();
    public DbSet<ContentBlock> ContentBlocks => Set<ContentBlock>();
    public DbSet<HeroSlide> HeroSlides => Set<HeroSlide>();
    public DbSet<HeroSlideTranslation> HeroSlideTranslations => Set<HeroSlideTranslation>();

    public DbSet<MembershipPlan> MembershipPlans => Set<MembershipPlan>();
    public DbSet<Membership> Memberships => Set<Membership>();
    public DbSet<MembershipPayment> MembershipPayments => Set<MembershipPayment>();

    public DbSet<Ambassador> Ambassadors => Set<Ambassador>();
    public DbSet<ReferralVisit> ReferralVisits => Set<ReferralVisit>();

    public DbSet<Seminar> Seminars => Set<Seminar>();
    public DbSet<SeminarTranslation> SeminarTranslations => Set<SeminarTranslation>();
    public DbSet<SeminarMedia> SeminarMedia => Set<SeminarMedia>();
    public DbSet<SeminarEnrollment> SeminarEnrollments => Set<SeminarEnrollment>();

    public DbSet<Notification> Notifications => Set<Notification>();
    public DbSet<JournalPost> JournalPosts => Set<JournalPost>();
    public DbSet<CommunityLink> CommunityLinks => Set<CommunityLink>();

    public DbSet<EmailLog> EmailLogs => Set<EmailLog>();
    public DbSet<AuditLogEntry> AuditLogEntries => Set<AuditLogEntry>();
    public DbSet<ConsentRecord> ConsentRecords => Set<ConsentRecord>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(VIHouseDbContext).Assembly);

        // Backs EfBookingRepository.GenerateNextReferenceAsync — a DB-level sequence guarantees
        // two simultaneous webhook deliveries can never allocate the same booking reference.
        modelBuilder.HasSequence<long>("BookingRefSeq", schema: "dbo").StartsAt(1).IncrementsBy(1);
    }
}
