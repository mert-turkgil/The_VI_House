using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using VIHouse.DataAccess.Identity;
using VIHouse.Entities.Applications;
using VIHouse.Entities.Commerce;
using VIHouse.Entities.Content;
using VIHouse.Entities.Experiences;
using VIHouse.Entities.Membership;
using VIHouse.Entities.Referrals;

namespace VIHouse.DataAccess.Concrete.EntityFramework.Seed;

/// <summary>
/// Development-only seed data: admin roles, one SuperAdmin account, a handful of sample
/// Experiences, and the homepage ContentPage with a ContentBlock per section. Testimonial/stat
/// content is deliberately placeholder-labelled rather than invented numbers/claims, per the
/// brief's "no fake testimonials/claims until real ones exist" rule (§15) — admins replace these
/// through the CMS once real figures/quotes are available.
/// </summary>
public static class DbSeeder
{
    public static async Task SeedAsync(
        VIHouseDbContext db,
        UserManager<ApplicationUser> userManager,
        RoleManager<ApplicationRole> roleManager,
        string seedAdminEmail,
        string seedAdminPassword)
    {
        await db.Database.MigrateAsync();

        await SeedRolesAsync(roleManager);
        await SeedAdminUserAsync(userManager, seedAdminEmail, seedAdminPassword);
        await SeedExperiencesAsync(db);
        await db.SaveChangesAsync(); // commit Experiences first — SeedApplicationsAsync looks them up by slug

        await SeedHomepageContentAsync(db);
        await SeedApplicationsAsync(db);
        await SeedMembershipPlansAsync(db);
        await db.SaveChangesAsync(); // commit before SeedAmbassadorsAsync, which needs Roles.Ambassador to already exist

        await SeedAmbassadorsAsync(db, userManager);

        await db.SaveChangesAsync();
    }

    private static async Task SeedAmbassadorsAsync(VIHouseDbContext db, UserManager<ApplicationUser> userManager)
    {
        if (await db.Ambassadors.AnyAsync())
            return;

        const string email = "anton@example.com";
        var user = await userManager.FindByEmailAsync(email);
        if (user is null)
        {
            user = new ApplicationUser
            {
                UserName = email,
                Email = email,
                EmailConfirmed = true,
                FirstName = "Anton",
                LastName = "Weber",
                Country = "DE",
                MemberStatus = Entities.Users.MemberStatus.Active,
            };

            var result = await userManager.CreateAsync(user, "Ambassador-Demo-2026!");
            if (!result.Succeeded) return;
        }

        if (!await userManager.IsInRoleAsync(user, Roles.Ambassador))
            await userManager.AddToRoleAsync(user, Roles.Ambassador);

        db.Ambassadors.Add(new Ambassador
        {
            UserId = user.Id,
            Code = "ANTON",
            Name = "Anton Weber",
            CommissionPercent = 15m,
            Status = AmbassadorStatus.Active,
        });
    }

    private static async Task SeedMembershipPlansAsync(VIHouseDbContext db)
    {
        if (await db.MembershipPlans.AnyAsync())
            return;

        db.MembershipPlans.AddRange(
            new MembershipPlan
            {
                Name = "Member",
                Description = "Standing access to The VI House community between experiences.",
                PriceMinor = 60000, // £600.00
                Currency = "GBP",
                BillingPeriod = MembershipBillingPeriod.Annual,
                Features = "Member Directory access\nPriority invitations to future experiences\nMonthly House Notes",
                Status = MembershipPlanStatus.Active,
                SortOrder = 1,
            },
            new MembershipPlan
            {
                Name = "Founding Member",
                Description = "For the earliest backers of The VI House — locked-in pricing and a permanent mark of when you joined.",
                PriceMinor = 150000, // £1,500.00
                Currency = "GBP",
                BillingPeriod = MembershipBillingPeriod.Annual,
                Features = "Everything in Member\nFounding Member badge on your profile\nFirst access to new city launches\nAnnual founder dinner",
                Status = MembershipPlanStatus.Active,
                SortOrder = 2,
            },
            new MembershipPlan
            {
                Name = "Partner",
                Description = "For operators and investors who want a standing seat in the room, not just a single experience.",
                PriceMinor = 350000, // £3,500.00
                Currency = "GBP",
                BillingPeriod = MembershipBillingPeriod.Annual,
                Features = "Everything in Founding Member\nComplimentary guest pass to one experience per year\nDirect line to the VI House team",
                Status = MembershipPlanStatus.Active,
                SortOrder = 3,
            });
    }

    private static async Task SeedRolesAsync(RoleManager<ApplicationRole> roleManager)
    {
        foreach (var roleName in Roles.All)
        {
            if (!await roleManager.RoleExistsAsync(roleName))
                await roleManager.CreateAsync(new ApplicationRole(roleName));
        }
    }

    private static async Task SeedAdminUserAsync(UserManager<ApplicationUser> userManager, string email, string password)
    {
        if (await userManager.FindByEmailAsync(email) is not null)
            return;

        var admin = new ApplicationUser
        {
            UserName = email,
            Email = email,
            EmailConfirmed = true,
            FirstName = "VI House",
            LastName = "Admin",
            Country = "GB",
            MemberStatus = Entities.Users.MemberStatus.Active,
        };

        var result = await userManager.CreateAsync(admin, password);
        if (result.Succeeded)
            await userManager.AddToRoleAsync(admin, Roles.SuperAdmin);
    }

    private static async Task SeedExperiencesAsync(VIHouseDbContext db)
    {
        if (await db.Experiences.AnyAsync())
            return;

        var now = DateTimeOffset.UtcNow;

        var izmir = new Experience
        {
            Slug = "izmir-founder-experience-2026",
            Title = "The VI House — Izmir",
            ShortSummary = "A five-day founder experience on the Aegean coast.",
            Description = "A curated gathering of founders, operators and investors for five days of sessions, workshops and private dinners.",
            City = "Izmir",
            Country = "TR",
            Venue = "To be confirmed on approval",
            TimeZoneId = "Europe/Istanbul",
            StartAtUtc = now.AddMonths(2),
            EndAtUtc = now.AddMonths(2).AddDays(5),
            Capacity = 25,
            Status = ExperienceStatus.ApplicationsOpen,
            Visibility = ExperienceVisibility.Public,
            IsSignature = true,
            SortOrder = 1,
        };
        izmir.TicketTypes.Add(new TicketType { ExperienceId = izmir.Id, Title = "Standard Experience", PriceMinor = 189900, Currency = "EUR", Inventory = 18, SortOrder = 1 });
        izmir.TicketTypes.Add(new TicketType { ExperienceId = izmir.Id, Title = "House Experience", PriceMinor = 289900, Currency = "EUR", Inventory = 7, SortOrder = 2 });
        izmir.Faqs.Add(new ExperienceFaq { ExperienceId = izmir.Id, Question = "What's included?", Answer = "Sessions, workshops, meals during the programme and community access. See the Included/Not Included section for full detail.", SortOrder = 1 });
        izmir.Inclusions.Add(new ExperienceInclusion { ExperienceId = izmir.Id, Text = "All programme sessions and workshops", IsIncluded = true, SortOrder = 1 });
        izmir.Inclusions.Add(new ExperienceInclusion { ExperienceId = izmir.Id, Text = "Flights", IsIncluded = false, SortOrder = 2 });

        var founderSession = new ExperienceProgramDay { ExperienceId = izmir.Id, DayNumber = 1, DateLabel = "Day 1", Title = "Arrival" };
        founderSession.Sessions.Add(new ExperienceSession { ProgramDayId = founderSession.Id, StartTime = new TimeSpan(19, 0, 0), EndTime = new TimeSpan(21, 0, 0), Title = "Welcome Dinner", SortOrder = 1 });
        izmir.ProgramDays.Add(founderSession);

        var london = new Experience
        {
            Slug = "london-growth-mastermind-2026",
            Title = "The VI House — London",
            ShortSummary = "A private growth mastermind session for operators scaling past seven figures.",
            Description = "A single-day, invite-shaped mastermind session with a small group of operators, hosted in central London.",
            City = "London",
            Country = "GB",
            Venue = "To be confirmed on approval",
            TimeZoneId = "Europe/London",
            StartAtUtc = now.AddMonths(1),
            EndAtUtc = now.AddMonths(1).AddHours(8),
            Capacity = 20,
            Status = ExperienceStatus.ApplicationsOpen,
            Visibility = ExperienceVisibility.Public,
            IsSignature = true,
            SortOrder = 2,
        };
        london.TicketTypes.Add(new TicketType { ExperienceId = london.Id, Title = "Standard Experience", PriceMinor = 99900, Currency = "GBP", Inventory = 20, SortOrder = 1 });

        var zurich = new Experience
        {
            Slug = "zurich-founder-dinner-2026",
            Title = "The VI House — Zurich",
            ShortSummary = "A private founder dinner in Zurich.",
            Description = "An intimate dinner session for founders across fintech, e-commerce and AI, hosted in Zurich.",
            City = "Zurich",
            Country = "CH",
            Venue = "To be confirmed on approval",
            TimeZoneId = "Europe/Zurich",
            StartAtUtc = now.AddDays(20),
            EndAtUtc = now.AddDays(20).AddHours(4),
            Capacity = 16,
            Status = ExperienceStatus.ComingSoon,
            Visibility = ExperienceVisibility.Public,
            SortOrder = 3,
        };
        zurich.TicketTypes.Add(new TicketType { ExperienceId = zurich.Id, Title = "Standard Experience", PriceMinor = 49900, Currency = "CHF", Inventory = 16, SortOrder = 1 });

        var lisbon = new Experience
        {
            Slug = "lisbon-founder-retreat-2026",
            Title = "The VI House — Lisbon",
            ShortSummary = "A four-day founder retreat on the Portuguese coast.",
            Description = "A small-group retreat for founders navigating growth-stage decisions, hosted in Lisbon.",
            City = "Lisbon",
            Country = "PT",
            Venue = "To be confirmed on approval",
            TimeZoneId = "Europe/Lisbon",
            StartAtUtc = now.AddMonths(3),
            EndAtUtc = now.AddMonths(3).AddDays(4),
            Capacity = 18,
            Status = ExperienceStatus.AlmostFull,
            Visibility = ExperienceVisibility.Public,
            SortOrder = 4,
        };
        lisbon.TicketTypes.Add(new TicketType { ExperienceId = lisbon.Id, Title = "Standard Experience", PriceMinor = 149900, Currency = "EUR", Inventory = 2, SortOrder = 1 });

        var singapore = new Experience
        {
            Slug = "singapore-growth-summit-2026",
            Title = "The VI House — Singapore",
            ShortSummary = "A growth-stage summit for founders building across Southeast Asia.",
            Description = "A two-day summit connecting founders and operators scaling across Southeast Asian markets.",
            City = "Singapore",
            Country = "SG",
            Venue = "To be confirmed on approval",
            TimeZoneId = "Asia/Singapore",
            StartAtUtc = now.AddMonths(4),
            EndAtUtc = now.AddMonths(4).AddDays(2),
            Capacity = 22,
            Status = ExperienceStatus.Waitlist,
            Visibility = ExperienceVisibility.Public,
            SortOrder = 5,
        };
        singapore.TicketTypes.Add(new TicketType { ExperienceId = singapore.Id, Title = "Standard Experience", PriceMinor = 129900, Currency = "USD", Inventory = 0, SortOrder = 1 });

        var miami = new Experience
        {
            Slug = "miami-founder-weekend-2025",
            Title = "The VI House — Miami",
            ShortSummary = "A private founder weekend in Miami.",
            Description = "An intimate weekend gathering for founders across fintech and consumer, hosted in Miami.",
            City = "Miami",
            Country = "US",
            Venue = "The Confidante, Miami Beach",
            TimeZoneId = "America/New_York",
            StartAtUtc = now.AddMonths(-2),
            EndAtUtc = now.AddMonths(-2).AddDays(3),
            Capacity = 20,
            Status = ExperienceStatus.Completed,
            Visibility = ExperienceVisibility.Public,
            SortOrder = 6,
        };
        miami.TicketTypes.Add(new TicketType { ExperienceId = miami.Id, Title = "Standard Experience", PriceMinor = 119900, Currency = "USD", Inventory = 0, SortOrder = 1 });

        // Deliberately Draft + Visibility=Public — proves an admin mid-edit can never leak this
        // onto the public site, listing, or homepage signature grid (see EfExperienceRepository's
        // unconditional Status != Draft filters and ExperiencesController.Details' explicit check).
        var berlinDraft = new Experience
        {
            Slug = "berlin-founder-summit-2026",
            Title = "The VI House — Berlin",
            ShortSummary = "Draft — not yet ready for review.",
            Description = "Draft description, still being written by the events team.",
            City = "Berlin",
            Country = "DE",
            TimeZoneId = "Europe/Berlin",
            StartAtUtc = now.AddMonths(5),
            EndAtUtc = now.AddMonths(5).AddDays(3),
            Capacity = 20,
            Status = ExperienceStatus.Draft,
            Visibility = ExperienceVisibility.Public,
            IsSignature = true, // also proves GetSignatureAsync excludes Draft even when flagged signature
            SortOrder = 7,
        };

        db.Experiences.AddRange(izmir, london, zurich, lisbon, singapore, miami, berlinDraft);

        // A live promo code against Izmir — 10% off, capped at 25 redemptions — demonstrates the
        // PromoCode -> PaymentService.TryApplyPromoAsync path without needing an admin CMS screen yet.
        db.PromoCodes.Add(new PromoCode
        {
            Code = "FOUNDER10",
            Type = PromoCodeType.Percentage,
            Value = 10,
            ExperienceId = izmir.Id,
            MaxRedemptions = 25,
            IsActive = true,
        });
    }

    private static async Task SeedHomepageContentAsync(VIHouseDbContext db)
    {
        if (await db.ContentPages.AnyAsync(p => p.Slug == "home"))
            return;

        var home = new ContentPage
        {
            Slug = "home",
            Title = "The VI House",
            MetaDescription = "A private global community for the next generation of founders.",
            IsPublished = true,
        };

        home.Blocks.Add(new ContentBlock
        {
            PageId = home.Id,
            SectionKey = "hero",
            SortOrder = 1,
            Heading = "Where ambition meets alignment.",
            Subheading = "A private global community for online founders, investors and operators.",
            CtaLabel = "Request Access",
            CtaUrl = "/apply",
        });

        home.Blocks.Add(new ContentBlock
        {
            PageId = home.Id,
            SectionKey = "feature-strip",
            SortOrder = 2,
            Heading = "Find what matters to you",
            ExtraJson = """
            [
              {"label":"Learn","description":"From founders and operators who have built what you are building."},
              {"label":"Connect","description":"Meet people beyond your existing circle."},
              {"label":"Grow","description":"Access frameworks and strategies to scale."},
              {"label":"Experience","description":"Join gatherings that create lasting relationships."},
              {"label":"Belong","description":"The House continues beyond the event."}
            ]
            """,
        });

        home.Blocks.Add(new ContentBlock
        {
            PageId = home.Id,
            SectionKey = "ecosystem",
            SortOrder = 3,
            Heading = "More than retreats. A complete network.",
            BodyText = "Experiences are the beginning. Everything you need to learn, connect and build sits in one private place.",
            CtaLabel = "Explore All Experiences",
            CtaUrl = "/experiences",
            ExtraJson = """
            [
              {"title":"Signature Experiences","description":"Small, curated in-person gatherings for founders, operators and investors across the world's most interesting cities."},
              {"title":"Application-First Access","description":"Every room is reviewed by hand. No open checkout — access is earned through a short application."},
              {"title":"A Private Member Portal","description":"Track applications, manage bookings and hold your profile in one private account, built to grow with the House."}
            ]
            """,
        });

        home.Blocks.Add(new ContentBlock
        {
            PageId = home.Id,
            SectionKey = "stats",
            SortOrder = 4,
            // Placeholder — replace with real, admin-verified figures once available (brief §15).
            ExtraJson = """
            [
              {"value":"—","label":"Members"},
              {"value":"—","label":"Countries"},
              {"value":"—","label":"Experiences"},
              {"value":"—","label":"Experts"}
            ]
            """,
        });

        home.Blocks.Add(new ContentBlock
        {
            PageId = home.Id,
            SectionKey = "trust",
            SortOrder = 5,
            Heading = "A community that builds legacy.",
            BodyText = "Member stories will appear here as the first Experiences take place.",
            ExtraJson = "[]",
        });

        db.ContentPages.Add(home);
        await Task.CompletedTask;
    }

    /// <summary>Sample applications in a few different funnel states, so the admin review screen isn't empty on first run.</summary>
    private static async Task SeedApplicationsAsync(VIHouseDbContext db)
    {
        if (await db.Applications.AnyAsync())
            return;

        var izmir = await db.Experiences.FirstOrDefaultAsync(e => e.Slug == "izmir-founder-experience-2026");
        var london = await db.Experiences.FirstOrDefaultAsync(e => e.Slug == "london-growth-mastermind-2026");
        if (izmir is null || london is null)
            return;

        var now = DateTimeOffset.UtcNow;

        db.Applications.Add(new Application
        {
            ExperienceId = izmir.Id,
            FirstName = "Elif",
            LastName = "Aydin",
            Email = "elif.aydin@example.com",
            Country = "TR",
            City = "Istanbul",
            CompanyName = "Aydin Ventures",
            JobTitle = "Founder & CEO",
            Industry = "Fintech",
            CompanyStage = "Series A",
            YearsOfExperience = 8,
            MotivationStatement = "Looking to connect with other founders scaling past their first big raise.",
            BuildingStatement = "A payments infrastructure platform for SME lending across the Middle East.",
            Status = ApplicationStatus.Submitted,
            SubmittedAt = now.AddDays(-1),
        });

        db.Applications.Add(new Application
        {
            ExperienceId = izmir.Id,
            FirstName = "Marcus",
            LastName = "Webb",
            Email = "marcus.webb@example.com",
            Country = "GB",
            City = "London",
            CompanyName = "Webb & Co",
            JobTitle = "Managing Partner",
            Industry = "Venture Capital",
            CompanyStage = "N/A",
            YearsOfExperience = 15,
            MotivationStatement = "Want direct access to the founders VI House curates before they raise.",
            BuildingStatement = "An early-stage fund focused on operator-led fintech and infrastructure.",
            Status = ApplicationStatus.UnderReview,
            SubmittedAt = now.AddDays(-3),
            ReviewedAt = now.AddDays(-2),
        });

        db.Applications.Add(new Application
        {
            ExperienceId = london.Id,
            FirstName = "Priya",
            LastName = "Shah",
            Email = "priya.shah@example.com",
            Country = "US",
            City = "New York",
            CompanyName = "Northline",
            JobTitle = "Co-Founder",
            Industry = "E-commerce",
            CompanyStage = "Bootstrapped",
            YearsOfExperience = 6,
            MotivationStatement = "Ready to plug into a room of operators who've actually scaled past 7 figures.",
            BuildingStatement = "A DTC logistics platform doing low-eight-figures ARR.",
            Status = ApplicationStatus.Shortlisted,
            SubmittedAt = now.AddDays(-5),
            ReviewedAt = now.AddDays(-4),
        });

        await Task.CompletedTask;
    }
}
