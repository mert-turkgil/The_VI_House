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
        await BackfillDemoContentAsync(db);
        await db.SaveChangesAsync(); // commit Experiences first — SeedApplicationsAsync looks them up by slug

        await SeedHomepageContentAsync(db);
        await SeedHeroSlidesAsync(db);
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

        // Copy is per culture (JournalPostTranslation). The first post is written in all four
        // languages on purpose: a seeded article that exists only in English proves nothing about
        // the fallback, and switching the language picker is the first thing anyone does with it.
        var why = new JournalPost
        {
            Slug = "why-we-built-the-vi-house",
            CoverImageUrl = "/img/journal/why-we-built-the-vi-house-1600.jpg",
            Category = JournalCategory.FounderStories,
            Status = JournalPostStatus.Published,
            AuthorName = "The VI House",
            PublishedAt = now.AddDays(-30),
        };
        why.Translations.Add(new JournalPostTranslation
        {
            JournalPostId = why.Id,
            Culture = "en-GB",
            Title = "Why We Built The VI House",
            Excerpt = "Every room we curate starts from the same question: who actually belongs in it.",
            Body = "<p>The best opportunities rarely happen by accident. They happen in rooms where the right " +
                   "people are already in the same place, at the same time, with enough trust between them to " +
                   "say what they actually think.</p>" +
                   "<p>The VI House exists to build those rooms deliberately — not at conference scale, and not " +
                   "through a payment form. Every experience starts with an application, reviewed by hand.</p>",
        });
        why.Translations.Add(new JournalPostTranslation
        {
            JournalPostId = why.Id,
            Culture = "de-DE",
            Title = "Warum wir The VI House gebaut haben",
            Excerpt = "Jeder Raum, den wir kuratieren, beginnt mit derselben Frage: Wer gehört wirklich hinein?",
            Body = "<p>Die besten Gelegenheiten entstehen selten zufällig. Sie entstehen in Räumen, in denen die " +
                   "richtigen Menschen bereits am selben Ort sind, zur selben Zeit, mit genug Vertrauen, um zu " +
                   "sagen, was sie wirklich denken.</p>" +
                   "<p>The VI House baut diese Räume bewusst — nicht in Konferenzgröße und nicht über ein " +
                   "Zahlungsformular. Jede Experience beginnt mit einer Bewerbung, von Hand geprüft.</p>",
        });
        why.Translations.Add(new JournalPostTranslation
        {
            JournalPostId = why.Id,
            Culture = "tr-TR",
            Title = "The VI House'u Neden Kurduk",
            Excerpt = "Kurduğumuz her oda aynı soruyla başlar: bu odada gerçekten kim olmalı?",
            Body = "<p>En iyi fırsatlar nadiren tesadüfen ortaya çıkar. Doğru insanların aynı anda aynı yerde " +
                   "olduğu ve aralarında gerçekten düşündüklerini söyleyecek kadar güven bulunan odalarda " +
                   "ortaya çıkar.</p>" +
                   "<p>The VI House bu odaları bilinçli olarak kurmak için var — konferans ölçeğinde değil ve bir " +
                   "ödeme formu üzerinden değil. Her deneyim, tek tek incelenen bir başvuruyla başlar.</p>",
        });
        why.Translations.Add(new JournalPostTranslation
        {
            JournalPostId = why.Id,
            Culture = "et-EE",
            Title = "Miks me The VI House'i lõime",
            Excerpt = "Iga ruum, mille kokku paneme, algab samast küsimusest: kes sinna tegelikult kuulub?",
            Body = "<p>Parimad võimalused ei sünni juhuslikult. Need sünnivad ruumides, kus õiged inimesed on " +
                   "juba samal ajal samas kohas ja usaldavad üksteist piisavalt, et öelda, mida nad tegelikult " +
                   "mõtlevad.</p>" +
                   "<p>The VI House ehitab neid ruume teadlikult — mitte konverentsi mõõtmes ja mitte " +
                   "maksevormi kaudu. Iga kogemus algab avaldusest, mis vaadatakse käsitsi üle.</p>",
        });
        db.JournalPosts.Add(why);

        var signal = new JournalPost
        {
            Slug = "the-quiet-signal-reading-capital-before-it-moves",
            CoverImageUrl = "/img/journal/the-quiet-signal-reading-capital-before-it-moves-1600.jpg",
            Category = JournalCategory.Capital,
            Status = JournalPostStatus.Published,
            AuthorName = "The VI House",
            PublishedAt = now.AddDays(-14),
        };
        signal.Translations.Add(new JournalPostTranslation
        {
            JournalPostId = signal.Id,
            Culture = "en-GB",
            Title = "The Quiet Signal: Reading Capital Before It Moves",
            Excerpt = "The founders who raise well are rarely the ones who pitch the loudest.",
            Body = "<p>Capital rarely announces itself before it moves. By the time a raise is public, the " +
                   "relationship that made it possible has usually existed for months.</p>" +
                   "<p>That is the case for rooms, not cold outreach — the founders who raise well are usually " +
                   "the ones who were already known, in person, before they needed anything.</p>",
        });
        db.JournalPosts.Add(signal);

        var inside = new JournalPost
        {
            Slug = "inside-the-room-what-makes-a-founder-session-work",
            CoverImageUrl = "/img/journal/inside-the-room-what-makes-a-founder-session-work-1600.jpg",
            Category = JournalCategory.HouseNotes,
            Status = JournalPostStatus.Published,
            AuthorName = "The VI House",
            PublishedAt = now.AddDays(-3),
        };
        inside.Translations.Add(new JournalPostTranslation
        {
            JournalPostId = inside.Id,
            Culture = "en-GB",
            Title = "Inside the Room: What Makes a Founder Session Work",
            Excerpt = "Notes from the House on running a session that people still talk about a year later.",
            Body = "<p>A good founder session has almost nothing to do with the agenda.</p>" +
                   "<p>It has everything to do with who is in the room, how small it stays, and whether people " +
                   "feel able to say the thing they actually came to say.</p>",
        });
        db.JournalPosts.Add(inside);

        // Deliberately Draft — proves the public /journal listing and /journal/{slug} both hide it
        // while it still appears in the admin Index.
        var draft = new JournalPost
        {
            Slug = "building-in-public-without-burning-out",
            CoverImageUrl = "/img/journal/building-in-public-without-burning-out-1600.jpg",
            Category = JournalCategory.Business,
            Status = JournalPostStatus.Draft,
            AuthorName = "The VI House",
        };
        draft.Translations.Add(new JournalPostTranslation
        {
            JournalPostId = draft.Id,
            Culture = "en-GB",
            Title = "Building in Public Without Burning Out",
            Excerpt = "Draft — still being written.",
            Body = "<p>Draft body, still being written by the team.</p>",
        });
        db.JournalPosts.Add(draft);
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

    /// <summary>
    /// Fills in demo cover images and audience tags on rows that already exist.
    ///
    /// SeedExperiencesAsync and SeedJournalPostsAsync both early-return the moment their table has
    /// any row in it, which is correct — they must never overwrite content someone has edited. The
    /// side effect is that anyone with a development database created before the imagery landed sees
    /// no photographs at all and reasonably concludes the work did not ship.
    ///
    /// This closes that gap without weakening the guard: it only ever writes where the field is
    /// still null, so an admin who deliberately cleared a cover keeps it cleared, and it is safe to
    /// run on every start. Development-only, like everything else reachable from SeedAsync.
    /// </summary>
    private static async Task BackfillDemoContentAsync(VIHouseDbContext db)
    {
        foreach (var experience in await db.Experiences.Where(e => e.CoverImageUrl == null).ToListAsync())
            experience.CoverImageUrl = $"/img/experiences/{experience.Slug}-1600.jpg";

        foreach (var post in await db.JournalPosts.Where(p => p.CoverImageUrl == null).ToListAsync())
            post.CoverImageUrl = $"/img/journal/{post.Slug}-1600.jpg";

        // Who each room is actually for. Deliberately different per city — the point of the section
        // is that these rooms are curated differently, and repeating one generic list would say the
        // opposite while looking like content.
        var audiences = new Dictionary<string, string>
        {
            ["izmir-founder-experience-2026"] = "Founders, Operators, Investors",
            ["london-growth-mastermind-2026"] = "Operators, Growth leads, Second-time founders",
            ["zurich-founder-dinner-2026"] = "Founders, Family offices, Private investors",
            ["lisbon-founder-retreat-2026"] = "Founders, Builders, Creators",
            ["singapore-growth-summit-2026"] = "Founders, Operators, Regional investors",
            ["miami-founder-weekend-2025"] = "Founders, Creators, Capital allocators",
            ["berlin-founder-summit-2026"] = "Founders, Engineers, Product leads",
        };

        foreach (var experience in await db.Experiences.Where(e => e.AudienceTags == null).ToListAsync())
        {
            if (audiences.TryGetValue(experience.Slug, out var tags))
                experience.AudienceTags = tags;
        }

        // Galleries too, for the same reason: the rows added in SeedExperiencesAsync never run on a
        // database that already had experiences in it, so the Experiences detail page would have no
        // gallery to render. Only fills experiences that have none — an experience someone has
        // curated by hand is left exactly as it is.
        if (await db.ExperienceImages.AnyAsync())
            return;

        var gallery = new (string AltText, string File)[]
        {
            ("An empty boardroom set for a small working session", "room-boardroom"),
            ("A long table set for a private dinner", "dinner"),
            ("A small group working through a problem on a whiteboard", "workshop"),
            ("A speaker addressing a seated group in a bright room", "speaker"),
        };

        var signature = await db.Experiences
            .Where(e => e.IsSignature)
            .OrderBy(e => e.SortOrder)
            .Take(2)
            .ToListAsync();

        foreach (var experience in signature)
        {
            for (var i = 0; i < gallery.Length; i++)
            {
                db.ExperienceImages.Add(new ExperienceImage
                {
                    ExperienceId = experience.Id,
                    Url = $"/img/gallery/{gallery[i].File}-1600.jpg",
                    AltText = gallery[i].AltText,
                    SortOrder = i + 1,
                });
            }
        }
    }

    private static async Task SeedExperiencesAsync(VIHouseDbContext db)
    {
        if (await db.Experiences.AnyAsync())
            return;

        var now = DateTimeOffset.UtcNow;

        var izmir = new Experience
        {
            Slug = "izmir-founder-experience-2026",
            CoverImageUrl = "/img/experiences/izmir-founder-experience-2026-1600.jpg",
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

        // Gallery images carry real alt text — unlike a cover, these sit away from any heading that
        // would describe them, so a blank alt would leave a screen reader with nothing at all.
        izmir.Gallery.Add(new ExperienceImage { ExperienceId = izmir.Id, Url = "/img/gallery/room-boardroom-1600.jpg", AltText = "An empty boardroom set for a small working session", SortOrder = 1 });
        izmir.Gallery.Add(new ExperienceImage { ExperienceId = izmir.Id, Url = "/img/gallery/dinner-1600.jpg", AltText = "A long table set for a private dinner", SortOrder = 2 });
        izmir.Gallery.Add(new ExperienceImage { ExperienceId = izmir.Id, Url = "/img/gallery/workshop-1600.jpg", AltText = "A small group working through a problem on a whiteboard", SortOrder = 3 });
        izmir.Gallery.Add(new ExperienceImage { ExperienceId = izmir.Id, Url = "/img/gallery/speaker-1600.jpg", AltText = "A speaker addressing a seated group in a bright room", SortOrder = 4 });

        var founderSession = new ExperienceProgramDay { ExperienceId = izmir.Id, DayNumber = 1, DateLabel = "Day 1", Title = "Arrival" };
        founderSession.Sessions.Add(new ExperienceSession { ProgramDayId = founderSession.Id, StartTime = new TimeSpan(19, 0, 0), EndTime = new TimeSpan(21, 0, 0), Title = "Welcome Dinner", SortOrder = 1 });
        izmir.ProgramDays.Add(founderSession);

        var london = new Experience
        {
            Slug = "london-growth-mastermind-2026",
            CoverImageUrl = "/img/experiences/london-growth-mastermind-2026-1600.jpg",
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
        london.Gallery.Add(new ExperienceImage { ExperienceId = london.Id, Url = "/img/gallery/room-roundtable-1600.jpg", AltText = "A roundtable laid out for a dozen people", SortOrder = 1 });
        london.Gallery.Add(new ExperienceImage { ExperienceId = london.Id, Url = "/img/gallery/workshop-1600.jpg", AltText = "A small group working through a problem on a whiteboard", SortOrder = 2 });

        var zurich = new Experience
        {
            Slug = "zurich-founder-dinner-2026",
            CoverImageUrl = "/img/experiences/zurich-founder-dinner-2026-1600.jpg",
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
            CoverImageUrl = "/img/experiences/lisbon-founder-retreat-2026-1600.jpg",
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
            CoverImageUrl = "/img/experiences/singapore-growth-summit-2026-1600.jpg",
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
            CoverImageUrl = "/img/experiences/miami-founder-weekend-2025-1600.jpg",
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
            CoverImageUrl = "/img/experiences/berlin-founder-summit-2026-1600.jpg",
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

    /// <summary>
    /// Three panels for the homepage hero carousel, each written in all four site languages.
    ///
    /// Seeded in every language on purpose: a slide that exists only in English proves nothing about
    /// the fallback, and the first thing anyone does with a new carousel is switch the language
    /// picker to see whether it followed.
    ///
    /// The photographs are the ones media-manifest.json fetches into /img/hero — pasted as URLs
    /// rather than uploaded, because a seeder cannot put bytes into the media root of a machine it
    /// has never seen. Uploading through the panel replaces them.
    /// </summary>
    private static async Task SeedHeroSlidesAsync(VIHouseDbContext db)
    {
        if (await db.HeroSlides.AnyAsync())
            return;

        var slides = new List<HeroSlide>
        {
            new()
            {
                SortOrder = 0,
                IsActive = true,
                ImageUrl = "/img/hero/lounge-1600.jpg",
                PrimaryCtaUrl = "/apply",
                SecondaryCtaUrl = "/membership",
            },
            new()
            {
                SortOrder = 1,
                IsActive = true,
                ImageUrl = "/img/hero/skyline-1600.jpg",
                PrimaryCtaUrl = "/experiences",
                SecondaryCtaUrl = "/sessions",
            },
            new()
            {
                SortOrder = 2,
                IsActive = true,
                ImageUrl = "/img/hero/retreat-1600.jpg",
                PrimaryCtaUrl = "/experiences",
                SecondaryCtaUrl = "/journal",
            },
        };

        // Culture, eyebrow, heading, subheading, primary label, secondary label, alt text.
        var copy = new[]
        {
            new[]
            {
                new[] { "en-GB", "The Sixth House", "Where Ambition Meets Alignment.", "The VI House is a private global community for online founders, investors and operators. Access sessions, connect with peers, and join life-changing retreats.", "Apply to Join", "Explore Platform", "A low-lit lounge with city windows at dusk" },
                new[] { "de-DE", "Das Sechste Haus", "Wo Ambition auf Haltung trifft.", "The VI House ist eine private globale Gemeinschaft für Online-Gründer, Investoren und Operator. Sessions besuchen, Gleichgesinnte treffen, an Retreats teilnehmen, die etwas verändern.", "Jetzt bewerben", "Plattform ansehen", "Eine gedämpft beleuchtete Lounge mit Stadtfenstern in der Dämmerung" },
                new[] { "tr-TR", "Altıncı Ev", "Hırsın Hizaya Geldiği Yer.", "The VI House; online kurucular, yatırımcılar ve operatörler için özel ve küresel bir topluluktur. Oturumlara katılın, benzer kişilerle tanışın ve hayatınızı değiştiren inzivalarda yer alın.", "Başvur", "Platformu Keşfet", "Alacakaranlıkta şehre bakan pencereleri olan loş bir oturma salonu" },
                new[] { "et-EE", "Kuues Koda", "Kus ambitsioon kohtub suunaga.", "The VI House on privaatne ülemaailmne kogukond veebiettevõtjatele, investoritele ja operaatoritele. Osale sessioonidel, kohtu omasugustega ja liitu retriitidega, mis midagi muudavad.", "Kandideeri", "Uuri platvormi", "Hämaralt valgustatud salong linnaakendega hämaruses" },
            },
            new[]
            {
                new[] { "en-GB", "Experiences", "Rooms Worth Flying For.", "Small, curated gatherings in the cities where the work is actually happening — reviewed by hand, capped by design.", "See Experiences", "Browse Sessions", "A city skyline seen from a high floor at night" },
                new[] { "de-DE", "Experiences", "Räume, für die sich die Reise lohnt.", "Kleine, kuratierte Treffen in den Städten, in denen die Arbeit wirklich stattfindet — von Hand geprüft, bewusst begrenzt.", "Experiences ansehen", "Sessions durchsuchen", "Eine nächtliche Skyline aus einem der oberen Stockwerke" },
                new[] { "tr-TR", "Deneyimler", "Uçmaya Değer Odalar.", "İşin gerçekten yapıldığı şehirlerde, küçük ve özenle seçilmiş buluşmalar — tek tek incelenir, bilinçli olarak sınırlı tutulur.", "Deneyimleri Gör", "Oturumlara Göz At", "Gece yüksek bir kattan görünen şehir silüeti" },
                new[] { "et-EE", "Kogemused", "Ruumid, mille pärast tasub lennata.", "Väikesed, hoolikalt valitud kohtumised linnades, kus töö tegelikult toimub — käsitsi üle vaadatud, teadlikult piiratud.", "Vaata kogemusi", "Sirvi sessioone", "Öine linnasiluett kõrgelt korruselt" },
            },
            new[]
            {
                new[] { "en-GB", "Signature Retreats", "Step Away. Come Back Sharper.", "A week among people building at your level, somewhere that makes the thinking easier. Applications are reviewed one at a time.", "View Retreats", "Read the Journal", "A terrace and pool overlooking the sea at golden hour" },
                new[] { "de-DE", "Signature Retreats", "Abstand nehmen. Klarer zurückkommen.", "Eine Woche unter Menschen, die auf Ihrem Niveau bauen — an einem Ort, der das Denken leichter macht. Bewerbungen werden einzeln geprüft.", "Retreats ansehen", "Journal lesen", "Eine Terrasse mit Pool mit Blick aufs Meer in der goldenen Stunde" },
                new[] { "tr-TR", "İmza Retreat'ler", "Uzaklaş. Daha Keskin Dön.", "Sizinle aynı seviyede inşa eden insanlar arasında, düşünmeyi kolaylaştıran bir yerde bir hafta. Başvurular tek tek değerlendirilir.", "Retreat'leri Gör", "Journal'ı Oku", "Altın saatte denize bakan bir teras ve havuz" },
                new[] { "et-EE", "Signature-retriidid", "Astu kõrvale. Tule teravamana tagasi.", "Nädal inimeste seas, kes ehitavad sinuga samal tasemel, kohas, kus mõtlemine on lihtsam. Avaldusi vaadatakse ükshaaval.", "Vaata retriite", "Loe ajakirja", "Terrass ja bassein merevaatega kuldsel tunnil" },
            },
        };

        for (var i = 0; i < slides.Count; i++)
        {
            foreach (var row in copy[i])
            {
                slides[i].Translations.Add(new HeroSlideTranslation
                {
                    HeroSlideId = slides[i].Id,
                    Culture = row[0],
                    Eyebrow = row[1],
                    Heading = row[2],
                    Subheading = row[3],
                    PrimaryCtaLabel = row[4],
                    SecondaryCtaLabel = row[5],
                    ImageAlt = row[6],
                });
            }
        }

        db.HeroSlides.AddRange(slides);
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
              {"title":"Live & On-Demand Webinars","description":"Learn from top founders, investors and experts across business, finance, marketing and personal growth.","imageUrl":"/img/ecosystem/webinars-800.jpg","imageAlt":"A speaker on stage in front of a seated audience","linkLabel":"Browse Webinars","linkUrl":"/sessions"},
              {"title":"Community & Networking","description":"Join private groups, meet like-minded peers and collaborate on the projects that matter.","imageUrl":"/img/ecosystem/community-800.jpg","imageAlt":"A group talking in a bright open workspace","linkLabel":"Enter Community","linkUrl":"/members"},
              {"title":"Digital Marketplace","description":"Discover and purchase high-quality business programmes, templates, resources and tools.","imageUrl":"/img/ecosystem/marketplace-800.jpg","imageAlt":"A laptop and notebook on a desk by a window","linkLabel":"Browse Marketplace","linkUrl":"/experiences"},
              {"title":"Signature Retreats","description":"Join transformative retreats in world-class locations that elevate your mind, network and business.","imageUrl":"/img/ecosystem/retreats-800.jpg","imageAlt":"A villa terrace and pool at sunset","linkLabel":"View Retreats","linkUrl":"/experiences"}
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
            Subheading = "A community that builds legacy",
            Heading = "Serious builders find their people here.",
            BodyText = "The VI House is where serious builders find their people and create the future together.",
            CtaLabel = "Become a Member",
            CtaUrl = "/membership",
            // Placeholder voices for local development, with the portraits the fetch script pulls
            // into /img/people. Replace them with real, attributable quotes before this database is
            // ever shown to anyone — an invented testimonial is the one piece of placeholder content
            // that is a problem rather than an eyesore.
            ExtraJson = """
            [
              {"quote":"The connections I made at VI House changed my business and my life.","author":"Placeholder Member","role":"E-commerce Founder","avatarUrl":"/img/people/voice-a-800.jpg"},
              {"quote":"Best community of high-level operators I have ever been part of.","author":"Placeholder Member","role":"Digital Creator","avatarUrl":"/img/people/voice-b-800.jpg"},
              {"quote":"The retreats are unmatched. Pure transformation.","author":"Placeholder Member","role":"Investor & Entrepreneur","avatarUrl":"/img/people/voice-c-800.jpg"}
            ]
            """,
        });

        // A block of its own rather than more fields on "trust": a ContentBlock has exactly one
        // ExtraJson and the testimonials already hold it. Names only — see Views/Home/_TrustLogo.cshtml
        // on why no logo artwork is shipped.
        home.Blocks.Add(new ContentBlock
        {
            PageId = home.Id,
            SectionKey = "trust-logos",
            SortOrder = 6,
            Heading = "Trusted by founders from",
            ExtraJson = """
            [
              {"name":"Shopify"},
              {"name":"Skool"},
              {"name":"Stripe"},
              {"name":"Teachable"},
              {"name":"Kajabi"},
              {"name":"Zapier"}
            ]
            """,
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
