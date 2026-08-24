using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using VIHouse.DataAccess.Identity;
using VIHouse.Entities.Applications;
using VIHouse.Entities.Commerce;
using VIHouse.Entities.Community;
using VIHouse.Entities.Content;
using VIHouse.Entities.Experiences;
using VIHouse.Entities.Journal;
using VIHouse.Entities.Membership;
using VIHouse.Entities.Referrals;
using VIHouse.Entities.Seminars;

namespace VIHouse.DataAccess.Concrete.EntityFramework.Seed;

/// <summary>
/// Two distinct jobs, deliberately kept separate:
///
/// <see cref="SeedIdentityAsync"/> creates the admin roles and the bootstrap admin accounts. It is
/// safe in <em>any</em> environment and runs in Production too — without it a fresh deployment would
/// have no way to sign in, since Invite Admin needs an existing SuperAdmin to use it.
///
/// <see cref="SeedAsync"/> additionally loads demo content — sample Experiences, applications,
/// journal posts, a fake ambassador, placeholder community links. That is Development-only and must
/// never touch a real database; it would put invented events and a dummy referral partner in front
/// of real customers. Testimonial/stat content is placeholder-labelled rather than invented
/// numbers, per the brief's "no fake testimonials/claims" rule (§15).
/// </summary>
public static class DbSeeder
{
    /// <summary>
    /// Roles + bootstrap admin accounts only. Idempotent: an account whose email already exists is
    /// left untouched, so restarting the app never resets a password someone has since changed.
    /// </summary>
    public static async Task SeedIdentityAsync(
        UserManager<ApplicationUser> userManager,
        RoleManager<ApplicationRole> roleManager,
        IEnumerable<SeedAdminAccount> admins)
    {
        await SeedRolesAsync(roleManager);

        foreach (var admin in admins)
        {
            await SeedAdminUserAsync(userManager, admin);
        }
    }

    public static async Task SeedAsync(
        VIHouseDbContext db,
        UserManager<ApplicationUser> userManager,
        RoleManager<ApplicationRole> roleManager,
        IEnumerable<SeedAdminAccount> admins)
    {
        await db.Database.MigrateAsync();

        await SeedIdentityAsync(userManager, roleManager, admins);
        await SeedExperiencesAsync(db);
        await db.SaveChangesAsync(); // commit Experiences first — SeedApplicationsAsync looks them up by slug

        await SeedHomepageContentAsync(db);
        await SeedApplicationsAsync(db);
        await SeedMembershipPlansAsync(db);
        await SeedJournalPostsAsync(db);
        await SeedSeminarsAsync(db);
        await SeedCommunityLinksAsync(db);
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

    private static async Task SeedCommunityLinksAsync(VIHouseDbContext db)
    {
        if (await db.CommunityLinks.AnyAsync())
            return;

        // Placeholder destinations — replace the URLs under Admin → Community before launch. They're
        // seeded inactive so a demo invite can never be shown to a real member by accident.
        db.CommunityLinks.AddRange(
            new CommunityLink
            {
                Label = "The VI House Discord",
                Description = "The members' server — introductions, city channels, and everything between gatherings.",
                Url = "https://discord.gg/replace-me",
                Kind = CommunityLinkKind.Discord,
                IsActive = false,
                SortOrder = 1,
            },
            new CommunityLink
            {
                Label = "House Broadcast",
                Description = "Our members-only broadcast channel for announcements and live sessions.",
                Url = "https://example.com/replace-me",
                Kind = CommunityLinkKind.Broadcast,
                IsActive = false,
                SortOrder = 2,
            });
    }

    private static async Task SeedMembershipPlansAsync(VIHouseDbContext db)
    {
        if (await db.MembershipPlans.AnyAsync())
            return;

        db.MembershipPlans.AddRange(
            new MembershipPlan
            {
                Name = "Member — Monthly",
                Description = "The same access as annual membership, billed monthly instead of up front.",
                PriceMinor = 6000, // £60.00 / month
                Currency = "GBP",
                BillingPeriod = MembershipBillingPeriod.Monthly,
                Features = "Member Directory access\nCommunity Discord\nPriority invitations to future experiences",
                Status = MembershipPlanStatus.Active,
                SortOrder = 0,
            },
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

    /// <summary>
    /// Two demo sessions, Development only — one members-only and priced, one public and free, which
    /// between them exercise every branch of the enrolment gate without anyone having to author
    /// content first. Both carry English copy plus a Turkish translation, so the language fallback
    /// is visible (German and Estonian are deliberately left unwritten: those readers should see
    /// the English, and the admin index should show the gap).
    ///
    /// No media is seeded — those are real files on disk, and inventing rows pointing at storage
    /// keys that do not exist would put broken players in front of whoever is testing.
    /// </summary>
    private static async Task SeedSeminarsAsync(VIHouseDbContext db)
    {
        if (await db.Seminars.AnyAsync())
            return;

        var now = DateTimeOffset.UtcNow;

        var pricing = new Seminar
        {
            Slug = "pricing-for-founders",
            Status = SeminarStatus.Published,
            Visibility = SeminarVisibility.Members,
            HostName = "Placeholder Host",
            HostTitle = "Operator in residence",
            IsOnline = true,
            Location = "Online",
            TimeZoneId = "Europe/London",
            StartAtUtc = now.AddDays(12),
            EndAtUtc = now.AddDays(12).AddHours(2),
            Capacity = 40,
            PriceMinor = 12000,
            Currency = "GBP",
            IncludedWithMembership = true,
            PublishedAt = now.AddDays(-3),
            SortOrder = 1,
        };
        pricing.Translations.Add(new SeminarTranslation
        {
            SeminarId = pricing.Id,
            Culture = "en-GB",
            Title = "Pricing For Founders",
            Summary = "Two hours on how to set a price that reflects what you are actually worth, and how to raise one without losing the room.",
            BodyHtml = "<h2>What we cover</h2><p>Placeholder content for local development. Replace it before this database is ever shown to anyone.</p><p>The session runs live, and the recording stays here afterwards for everyone who enrolled.</p>",
        });
        pricing.Translations.Add(new SeminarTranslation
        {
            SeminarId = pricing.Id,
            Culture = "tr-TR",
            Title = "Kurucular İçin Fiyatlandırma",
            Summary = "Gerçekten ne değerde olduğunuzu yansıtan bir fiyat belirlemek ve odayı kaybetmeden fiyat yükseltmek üzerine iki saat.",
            BodyHtml = "<h2>Neleri ele alıyoruz</h2><p>Yerel geliştirme için yer tutucu içerik. Bu veritabanı birine gösterilmeden önce değiştirin.</p>",
        });

        var intro = new Seminar
        {
            Slug = "how-the-house-works",
            Status = SeminarStatus.Published,
            Visibility = SeminarVisibility.Public,
            HostName = "The VI House",
            IsOnline = true,
            TimeZoneId = "Europe/London",
            // No start date: on-demand content, which is the other half of what a Session can be.
            Capacity = 0,
            PriceMinor = 0,
            Currency = "GBP",
            IncludedWithMembership = true,
            PublishedAt = now.AddDays(-10),
            SortOrder = 2,
        };
        intro.Translations.Add(new SeminarTranslation
        {
            SeminarId = intro.Id,
            Culture = "en-GB",
            Title = "How The House Works",
            Summary = "A short introduction to the membership, the experiences and what the community is actually for.",
            BodyHtml = "<p>Placeholder content for local development. Free to anyone with an account, which makes it the quickest way to exercise the one-click enrolment path.</p>",
        });

        db.Seminars.AddRange(pricing, intro);
    }

    private static async Task SeedJournalPostsAsync(VIHouseDbContext db)
    {
        if (await db.JournalPosts.AnyAsync())
            return;

        var now = DateTimeOffset.UtcNow;

        db.JournalPosts.Add(new JournalPost
        {
            Title = "Why We Built The VI House",
            Slug = "why-we-built-the-vi-house",
            Category = JournalCategory.FounderStories,
            Status = JournalPostStatus.Published,
            Excerpt = "Every room we curate starts from the same question: who actually belongs in it.",
            Body = "The best opportunities rarely happen by accident. They happen in rooms where the right " +
                   "people are already in the same place, at the same time, with enough trust between them to " +
                   "say what they actually think.\n\n" +
                   "The VI House exists to build those rooms deliberately — not at conference scale, and not " +
                   "through a payment form. Every experience starts with an application, reviewed by hand.",
            AuthorName = "The VI House",
            PublishedAt = now.AddDays(-30),
        });

        db.JournalPosts.Add(new JournalPost
        {
            Title = "The Quiet Signal: Reading Capital Before It Moves",
            Slug = "the-quiet-signal-reading-capital-before-it-moves",
            Category = JournalCategory.Capital,
            Status = JournalPostStatus.Published,
            Excerpt = "The founders who raise well are rarely the ones who pitch the loudest.",
            Body = "Capital rarely announces itself before it moves. By the time a raise is public, the " +
                   "relationship that made it possible has usually existed for months.\n\n" +
                   "That's the case for rooms, not cold outreach — the founders who raise well are usually " +
                   "the ones who were already known, in person, before they needed anything.",
            AuthorName = "The VI House",
            PublishedAt = now.AddDays(-14),
        });

        db.JournalPosts.Add(new JournalPost
        {
            Title = "Inside the Room: What Makes a Founder Session Work",
            Slug = "inside-the-room-what-makes-a-founder-session-work",
            Category = JournalCategory.HouseNotes,
            Status = JournalPostStatus.Published,
            Excerpt = "Notes from the House on running a session that people still talk about a year later.",
            Body = "A good founder session has almost nothing to do with the agenda.\n\n" +
                   "It has everything to do with who's in the room, how small it stays, and whether people " +
                   "feel able to say the thing they actually came to say.",
            AuthorName = "The VI House",
            PublishedAt = now.AddDays(-3),
        });

        // Deliberately Draft — proves the public /journal listing and /journal/{slug} both hide it
        // while it still appears in the admin Index.
        db.JournalPosts.Add(new JournalPost
        {
            Title = "Building in Public Without Burning Out",
            Slug = "building-in-public-without-burning-out",
            Category = JournalCategory.Business,
            Status = JournalPostStatus.Draft,
            Excerpt = "Draft — still being written.",
            Body = "Draft body, still being written by the team.",
            AuthorName = "The VI House",
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

    private static async Task SeedAdminUserAsync(UserManager<ApplicationUser> userManager, SeedAdminAccount account)
    {
        // Existing account is left completely alone — no password reset, no role changes. Otherwise
        // every restart would undo a password the admin has since changed, and quietly re-grant a
        // role someone deliberately removed.
        if (await userManager.FindByEmailAsync(account.Email) is not null)
            return;

        var admin = new ApplicationUser
        {
            UserName = account.Email,
            Email = account.Email,
            // Pre-confirmed because there's nobody to send a confirmation link *from* on a fresh
            // deployment. Two-factor is still enforced on first sign-in by
            // OnboardingRequirementFilter, so the account is not usable until it's secured.
            EmailConfirmed = true,
            FirstName = account.FirstName ?? "VI House",
            LastName = account.LastName ?? "Admin",
            Country = "GB",
            MemberStatus = Entities.Users.MemberStatus.Active,
        };

        var result = await userManager.CreateAsync(admin, account.Password);
        if (result.Succeeded)
            await userManager.AddToRolesAsync(admin, account.EffectiveRoles);
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
            // Indicative figures, not live counts — edit them under Admin → Content once the real,
            // admin-verified numbers are available (brief §15). The homepage animates whatever
            // number is here; a non-numeric value is simply displayed as written.
            ExtraJson = """
            [
              {"value":"180+","label":"Members"},
              {"value":"24","label":"Countries"},
              {"value":"65+","label":"Experiences"},
              {"value":"40+","label":"Experts"}
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
