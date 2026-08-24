using System.Globalization;
using System.Threading.RateLimiting;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Localization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.Extensions.Options;
using Microsoft.EntityFrameworkCore;
using VIHouse.Business.Abstract;
using VIHouse.Business.Concrete;
using VIHouse.Business.Options;
using VIHouse.DataAccess.Abstract;
using VIHouse.DataAccess.Concrete.EntityFramework;
using VIHouse.DataAccess.Concrete.EntityFramework.Seed;
using VIHouse.DataAccess.Identity;
using VIHouse.WebUI;
using VIHouse.WebUI.Filters;
using VIHouse.WebUI.Services;

// Process-wide fallback only (background services like TicketHoldExpiryService run with no HTTP
// request, so there's no per-request culture to fall back to) — the real per-request culture for
// site chrome/static pages is chosen by UseRequestLocalization below, driven by a cookie set via
// CultureController (brief: EN/DE/TR/ET). Admin area and CMS/experience content stay en-GB only.
var defaultCulture = new CultureInfo("en-GB");
CultureInfo.DefaultThreadCurrentCulture = defaultCulture;
CultureInfo.DefaultThreadCurrentUICulture = defaultCulture;

// The list itself lives in Business/Options/SiteCultures, because the seminar layer resolves its
// per-culture content against it and cannot reference this project. CultureController and the nav's
// language switcher read the same table, so a fifth language is one entry, not four edits.
var supportedCultures = SiteCultures.Names;

var builder = WebApplication.CreateBuilder(args);

// --- Data access -----------------------------------------------------------------------------
var connectionString = builder.Configuration.GetConnectionString("DefaultConnection")
    ?? throw new InvalidOperationException("Connection string 'DefaultConnection' not found.");

builder.Services.AddDbContext<VIHouseDbContext>(options => options.UseSqlServer(connectionString));

// --- Identity ----------------------------------------------------------------------------------
// (The Identity UI scaffolder normally inserts its own AddDefaultIdentity<T> call here — removed
// in favor of the AddIdentity<ApplicationUser, ApplicationRole> setup below, which already covers
// everything AddDefaultIdentity does plus our custom role manager and password/lockout policy.)
builder.Services.AddIdentity<ApplicationUser, ApplicationRole>(options =>
    {
        // Explicit policy rather than relying on framework defaults (brief §94/§201).
        options.Password.RequiredLength = 10;
        options.Password.RequireNonAlphanumeric = true;
        options.Lockout.MaxFailedAccessAttempts = 5;
        options.Lockout.DefaultLockoutTimeSpan = TimeSpan.FromMinutes(15);
        options.SignIn.RequireConfirmedAccount = false; // Phase 1: email confirmation not yet wired to the email pipeline
    })
    .AddEntityFrameworkStores<VIHouseDbContext>()
    .AddDefaultTokenProviders()
    .AddDefaultUI();

// How quickly a revoked account actually loses its session. The framework default is 30 minutes:
// the auth cookie is taken at face value until then, so "reset this admin's two-factor" (see
// AdminUsersController.ResetTwoFactor) or a role removal would leave the target's existing browser
// session working for up to half an hour. On an invite-only platform where the panel exposes every
// member's payment history, "revoke now" has to mean closer to now than that.
//
// The trade is one extra user lookup per session per interval, which is negligible next to the
// per-request database reads the onboarding gate already does. Five minutes is the ceiling on the
// cookie alone; the gate itself re-reads two-factor state from the database on every authorized
// request, so the admin panel closes on the very next click regardless of this setting.
builder.Services.Configure<SecurityStampValidatorOptions>(options =>
    options.ValidationInterval = TimeSpan.FromMinutes(5));

// --- Data Protection ---------------------------------------------------------------------------
// Without this the key ring lives wherever ASP.NET Core's default picks (often the app folder, or
// nowhere durable at all under IIS with no loaded user profile), which means a redeploy or app-pool
// recycle silently invalidates every auth cookie, antiforgery token, and Identity-issued token —
// the exact failure mode behind "2FA is broken in production" reports. Keys must therefore live at
// a stable path *outside* the deployed app directory in Production (set DataProtection:KeysPath in
// appsettings.Production.json), so publishing over the app never wipes them. SetApplicationName is
// what lets the main site and the admin subdomain share one key ring: they're the same app, so the
// purpose strings must match even if the deployment layout ever splits them.
var keysPath = builder.Configuration["DataProtection:KeysPath"];
if (string.IsNullOrWhiteSpace(keysPath))
{
    keysPath = Path.Combine(builder.Environment.ContentRootPath, "App_Data", "dp-keys");
}

Directory.CreateDirectory(keysPath);
var dataProtection = builder.Services.AddDataProtection()
    .SetApplicationName("VIHouse")
    .PersistKeysToFileSystem(new DirectoryInfo(keysPath));

// PersistKeysToFileSystem turns off the automatic at-rest encryption that would otherwise apply, so
// without this the key ring sits on disk in plaintext — and anything that can read those files can
// forge an authentication cookie for any account. DPAPI is Windows-only; protectToLocalMachine
// scopes the key to the machine rather than the current user, so it still decrypts after an IIS app
// pool identity change. Non-Windows hosts fall through to plaintext and rely on filesystem
// permissions instead, which is the framework's own default there.
if (OperatingSystem.IsWindows())
{
    dataProtection.ProtectKeysWithDpapi(protectToLocalMachine: true);
}

// UseHttpsRedirection can't infer a port when the app isn't itself listening on HTTPS (typical
// behind a reverse proxy / IIS doing TLS termination), and logs "Failed to determine the https port
// for redirect" then skips redirecting entirely. Stating it explicitly makes the redirect actually
// work in Production; Development is left alone so the Kestrel launch profile keeps deciding.
if (!builder.Environment.IsDevelopment())
{
    var httpsPort = builder.Configuration.GetValue<int?>("Https:Port") ?? 443;
    builder.Services.AddHttpsRedirection(options => options.HttpsPort = httpsPort);
}

// HSTS: 1 year + subdomains + preload-ready, since Production genuinely serves both the main
// domain and the admin subdomain over HTTPS only. Framework default (30 days, no subdomains) is
// too easy to accidentally fall back to plain HTTP on the admin subdomain if it's ever missed.
builder.Services.AddHsts(options =>
{
    options.MaxAge = TimeSpan.FromDays(365);
    options.IncludeSubDomains = true;
    options.Preload = true;
});

// Only set when the admin panel lives on its own subdomain (see "AdminHost" below) — a shared
// cookie domain (e.g. ".thevihouse.com") lets one login work across both thevihouse.com and
// admin.thevihouse.com. Left unset in Development, where everything is on localhost anyway.
//
// HostAwareCookieManager is what stops this setting from being a trap. A browser silently discards
// a cookie whose Domain doesn't match the host it is talking to, so pointing this at a domain the
// deployment doesn't actually serve — a typo, or a production build being smoke-tested on
// localhost — makes every sign-in appear to succeed and then land the visitor back on the login
// page with nothing logged anywhere. The manager degrades a mismatch to a host-only cookie and
// warns, so the app stays usable and the misconfiguration is visible.
var cookieDomain = builder.Configuration["CookieDomain"];
if (!string.IsNullOrWhiteSpace(cookieDomain))
{
    builder.Services.ConfigureApplicationCookie(options => options.Cookie.Domain = cookieDomain);

    // Configured through the options pipeline rather than inside the call above, so the logger
    // comes from the real container — building a second provider here to resolve one would give
    // this manager its own duplicate singletons.
    builder.Services
        .AddOptions<CookieAuthenticationOptions>(IdentityConstants.ApplicationScheme)
        .Configure<ILoggerFactory>((options, loggerFactory) =>
            options.CookieManager = new HostAwareCookieManager(loggerFactory));
}

// Google Sign-In: an alternative way into an *existing* account (the admin's, today), never a way
// to create one — ExternalLogin.cshtml.cs rejects any Google account not already linked via
// Manage/ExternalLogins, so this can't be used to bypass the invite-only application-approval
// funnel (brief §25). Registered only when configured, so an environment with no
// Authentication:Google:ClientId/ClientSecret (missing user-secret in Development, blank
// placeholder in appsettings.Production.json) just doesn't show the button instead of crashing —
// same secrets policy as Stripe: user-secrets in Development, appsettings.Production.json on the
// server, never a committed file.
var googleClientId = builder.Configuration["Authentication:Google:ClientId"];
var googleClientSecret = builder.Configuration["Authentication:Google:ClientSecret"];
if (!string.IsNullOrWhiteSpace(googleClientId) && !string.IsNullOrWhiteSpace(googleClientSecret))
{
    builder.Services.AddAuthentication()
        .AddGoogle(options =>
        {
            options.ClientId = googleClientId;
            options.ClientSecret = googleClientSecret;
        });
}

// --- Uploaded media ----------------------------------------------------------------------------
// Seminar assets (recordings, stills, decks) live outside both wwwroot and the deployed app
// directory, for the same two reasons the data-protection key ring does: publishing over the app
// must not delete them, and nothing under this path may be reachable as a static file. They are
// streamed by SeminarsController, which checks the viewer's enrolment first — a members-only
// session's recording is not something to hand out to whoever receives the URL. MapStaticAssets
// would not have served them anyway: it only knows about files that existed at build time.
var mediaRoot = builder.Configuration["Media:RootPath"];
if (string.IsNullOrWhiteSpace(mediaRoot))
{
    mediaRoot = Path.Combine(builder.Environment.ContentRootPath, "App_Media");
}

Directory.CreateDirectory(mediaRoot);
builder.Services.Configure<MediaOptions>(options => options.RootPath = mediaRoot);

// --- Repositories (DataAccess.Abstract -> Concrete.EntityFramework) -----------------------------
// Open-generic fallback for entities that only ever need generic CRUD (no custom queries) — e.g.
// ExperienceInclusion/ExperienceFaq, used directly by ExperienceService for unambiguous Added-state
// inserts (see ExperienceService.AddTicketTypeAsync's comment for why this matters).
builder.Services.AddScoped(typeof(IRepository<>), typeof(EfRepository<>));

builder.Services.AddScoped<IExperienceRepository, EfExperienceRepository>();
builder.Services.AddScoped<ITicketTypeRepository, EfTicketTypeRepository>();
builder.Services.AddScoped<IApplicationRepository, EfApplicationRepository>();
builder.Services.AddScoped<IBookingRepository, EfBookingRepository>();
builder.Services.AddScoped<IPaymentRepository, EfPaymentRepository>();
builder.Services.AddScoped<IInvitationRepository, EfInvitationRepository>();
builder.Services.AddScoped<IPromoCodeRepository, EfPromoCodeRepository>();
builder.Services.AddScoped<ITicketHoldRepository, EfTicketHoldRepository>();
builder.Services.AddScoped<IWaitlistRepository, EfWaitlistRepository>();
builder.Services.AddScoped<IContentPageRepository, EfContentPageRepository>();
builder.Services.AddScoped<IEmailLogRepository, EfEmailLogRepository>();
builder.Services.AddScoped<IAuditLogRepository, EfAuditLogRepository>();
builder.Services.AddScoped<IProfileRepository, EfProfileRepository>();
builder.Services.AddScoped<IWebhookEventRepository, EfWebhookEventRepository>();
builder.Services.AddScoped<IMembershipPaymentRepository, EfMembershipPaymentRepository>();
builder.Services.AddScoped<IAmbassadorRepository, EfAmbassadorRepository>();
builder.Services.AddScoped<INotificationRepository, EfNotificationRepository>();
builder.Services.AddScoped<IJournalPostRepository, EfJournalPostRepository>();
builder.Services.AddScoped<ISeminarRepository, EfSeminarRepository>();
builder.Services.AddScoped<ISeminarEnrollmentRepository, EfSeminarEnrollmentRepository>();

// --- Business services -------------------------------------------------------------------------
builder.Services.AddScoped<IExperienceService, ExperienceService>();
builder.Services.AddScoped<IApplicationService, ApplicationService>();
builder.Services.AddScoped<ICapacityService, CapacityService>();
builder.Services.AddScoped<IPaymentService, PaymentService>();
builder.Services.AddScoped<IContentService, ContentService>();
builder.Services.AddScoped<IMembershipService, MembershipService>();
builder.Services.AddScoped<IAmbassadorService, AmbassadorService>();
builder.Services.AddScoped<INotificationService, NotificationService>();
builder.Services.AddScoped<IJournalService, JournalService>();
builder.Services.AddScoped<ISeminarService, SeminarService>();

// Local disk today; the interface exists so a move to blob storage/CDN is one new class.
builder.Services.AddScoped<IMediaStorage, LocalMediaStorage>();

// Stripe keys: user-secrets in Development, environment variables (or a real vault) in Production —
// never a committed appsettings.*.json file, same policy as SeedAdmin's credentials.
builder.Services.Configure<StripeOptions>(builder.Configuration.GetSection("Stripe"));
builder.Services.AddScoped<IPaymentProvider, StripePaymentProvider>();

// SMTP: Host/Port/FromName/FromEmail are plain config (appsettings.Development.json /
// appsettings.Production.json) since they aren't secret and change per environment. Username/Password
// are secret and come from user-secrets/environment variables only — see SmtpOptions.
builder.Services.Configure<SmtpOptions>(builder.Configuration.GetSection("Smtp"));
builder.Services.Configure<SiteOptions>(builder.Configuration.GetSection("Site"));
builder.Services.AddScoped<IEmailSender, SmtpEmailSender>();
builder.Services.AddScoped<IEmailTemplateRenderer, RazorEmailTemplateRenderer>();
builder.Services.AddScoped<IEmailService, EmailService>();

// Releases abandoned TicketHolds back to inventory every 60s (brief §177-179) — a safety net
// alongside the immediate release on checkout failure/expiry in PaymentService.
builder.Services.AddHostedService<TicketHoldExpiryService>();

// --- Localization (EN default / DE / TR / ET) --------------------------------------------------
// Cookie-driven, not URL-prefixed: switching language never changes the URL (thevihouse.com/about
// stays thevihouse.com/about in every language), which keeps every existing [Route] attribute
// untouched and avoids reworking routing across the whole controller set for this pass. Covers
// site chrome (nav/footer) plus the new About/FAQ/Contact/Legal/Account/Status pages — CMS and
// Experience content stay English-only for now (they're admin-authored content, not UI chrome).
// No ResourcesPath set: SharedResource.cs and its .resx files live in the same folder
// (Resources/), which makes MSBuild treat them as a "dependent" pair and name the embedded
// resource after the .cs file's namespace (VIHouse.WebUI.SharedResource.resources) rather than
// the folder path — so the lookup base name must match that, not "Resources.SharedResource".
builder.Services.AddLocalization();
builder.Services.Configure<RequestLocalizationOptions>(options =>
{
    options.SetDefaultCulture(SiteCultures.Default);
    options.AddSupportedCultures(supportedCultures);
    options.AddSupportedUICultures(supportedCultures);
    options.RequestCultureProviders = [new CookieRequestCultureProvider { CookieName = CookieRequestCultureProvider.DefaultCookieName }];
});

// --- MVC + Razor Pages (Identity UI is Razor-Pages-based) -----------------------------------------
builder.Services.AddControllersWithViews(options =>
{
    // Global, but self-scoping: the filter only acts on endpoints that require authorization, so
    // the public site and the apply/join/checkout path are untouched. Members and admins alike must
    // confirm their email and switch on 2FA before any signed-in page will render — including the
    // two bootstrap admin accounts on their very first sign-in after a deployment. Registered here
    // rather than on AddRazorPages because it is an IAsyncResourceFilter, which MvcOptions applies
    // to controllers and Razor Pages alike. See OnboardingRequirementFilter.
    options.Filters.Add(typeof(OnboardingRequirementFilter));

    // Defense-in-depth: every POST/PUT/DELETE/PATCH is antiforgery-checked by default now, not
    // just the ones that remembered to add [ValidateAntiForgeryToken]. The one deliberate
    // exception is the Stripe webhook, which opts out explicitly via [IgnoreAntiforgeryToken]
    // (it's a server-to-server call with no browser cookie/form to carry a token).
    options.Filters.Add(new AutoValidateAntiforgeryTokenAttribute());
})
    // Points [Required]/[StringLength]/[Display] at SharedResource, so a validation message can be
    // a resource key instead of a hard-coded English literal (see TwoFactorSetupViewModel). Keys
    // with no matching entry fall back to the key text itself, so every message already written as
    // plain English elsewhere is untouched by this.
    .AddDataAnnotationsLocalization(options =>
        options.DataAnnotationLocalizerProvider = (_, factory) => factory.Create(typeof(SharedResource)));

builder.Services.AddRazorPages();

// --- Rate limiting (brief-adjacent hardening: brute-force/spam protection on sensitive endpoints) ---
// Fixed-window, partitioned per client IP. QueueLimit 0 means excess requests are rejected
// immediately (429) rather than queued — right call for anti-abuse limits, wrong call for
// smoothing legitimate burst traffic, which isn't the goal here.
builder.Services.AddRateLimiter(options =>
{
    options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;

    static string PartitionKey(HttpContext ctx) => ctx.Connection.RemoteIpAddress?.ToString() ?? "unknown";

    options.AddPolicy("auth", ctx => RateLimitPartition.GetFixedWindowLimiter(PartitionKey(ctx), _ =>
        new FixedWindowRateLimiterOptions { PermitLimit = 10, Window = TimeSpan.FromMinutes(1), QueueLimit = 0 }));

    options.AddPolicy("application-submit", ctx => RateLimitPartition.GetFixedWindowLimiter(PartitionKey(ctx), _ =>
        new FixedWindowRateLimiterOptions { PermitLimit = 5, Window = TimeSpan.FromHours(1), QueueLimit = 0 }));

    options.AddPolicy("checkout", ctx => RateLimitPartition.GetFixedWindowLimiter(PartitionKey(ctx), _ =>
        new FixedWindowRateLimiterOptions { PermitLimit = 20, Window = TimeSpan.FromMinutes(1), QueueLimit = 0 }));
});

var app = builder.Build();

// Fail fast in Production rather than silently taking money-facing requests with no Stripe keys
// configured — Development is allowed to run without them (only checkout/webhook routes need them).
if (app.Environment.IsProduction())
{
    var stripeOptions = app.Services.GetRequiredService<IOptions<StripeOptions>>().Value;
    if (string.IsNullOrWhiteSpace(stripeOptions.SecretKey) || string.IsNullOrWhiteSpace(stripeOptions.WebhookSecret))
    {
        throw new InvalidOperationException(
            "Stripe:SecretKey / Stripe:WebhookSecret are not configured. Set them in the server's " +
            "appsettings.Production.json (gitignored — populated on the server, never committed) or " +
            "via environment variables (Stripe__SecretKey, Stripe__WebhookSecret).");
    }
}

// --- Seeding ------------------------------------------------------------------------------------
// Two different scopes on purpose. Development gets the full seed: migrations plus demo content
// (sample experiences, journal posts, a fake ambassador). Production gets ONLY roles and the
// bootstrap admin accounts — demo content must never appear in front of real customers, and
// migrations stay a deliberate deployment step rather than something that runs itself on boot.
{
    using var scope = app.Services.CreateScope();
    var userManager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
    var roleManager = scope.ServiceProvider.GetRequiredService<RoleManager<ApplicationRole>>();
    var seedLogger = scope.ServiceProvider.GetRequiredService<ILoggerFactory>().CreateLogger("Seed");

    var seedAdmins = ReadSeedAdmins(builder.Configuration);

    if (app.Environment.IsDevelopment())
    {
        if (seedAdmins.Count == 0)
            throw new InvalidOperationException(
                "No seed admin configured. Set SeedAdmin:Email/SeedAdmin:Password (or a SeedAdmins array) in user-secrets.");

        var db = scope.ServiceProvider.GetRequiredService<VIHouseDbContext>();
        await DbSeeder.SeedAsync(db, userManager, roleManager, seedAdmins);
    }
    else if (seedAdmins.Count > 0)
    {
        try
        {
            await DbSeeder.SeedIdentityAsync(userManager, roleManager, seedAdmins);
        }
        catch (Exception ex)
        {
            // Logged rather than thrown: a seeding problem (most likely a schema that hasn't had
            // migrations applied yet) shouldn't take the whole site down on boot. Existing admins
            // can still sign in; the log says what went wrong.
            seedLogger.LogCritical(ex, "Admin seeding failed. Check that migrations have been applied to the production database.");
        }
    }
    else
    {
        seedLogger.LogWarning(
            "No seed admin configured. If this is a fresh deployment nobody can sign in — set SeedAdmin or SeedAdmins in appsettings.Production.json.");
    }
}

/// Accepts either the original single "SeedAdmin" object or a "SeedAdmins" array (or both, deduped
/// by email). Keeping the singular form working means existing user-secrets setups don't break.
static List<SeedAdminAccount> ReadSeedAdmins(IConfiguration configuration)
{
    var accounts = new List<SeedAdminAccount>();

    var singleEmail = configuration["SeedAdmin:Email"];
    var singlePassword = configuration["SeedAdmin:Password"];
    if (!string.IsNullOrWhiteSpace(singleEmail) && !string.IsNullOrWhiteSpace(singlePassword))
    {
        accounts.Add(new SeedAdminAccount(
            singleEmail.Trim(), singlePassword,
            configuration["SeedAdmin:FirstName"], configuration["SeedAdmin:LastName"],
            configuration.GetSection("SeedAdmin:Roles").Get<string[]>()));
    }

    foreach (var section in configuration.GetSection("SeedAdmins").GetChildren())
    {
        var email = section["Email"];
        var password = section["Password"];
        if (string.IsNullOrWhiteSpace(email) || string.IsNullOrWhiteSpace(password))
            continue; // a placeholder entry left blank in the template — skip, don't fail

        accounts.Add(new SeedAdminAccount(
            email.Trim(), password,
            section["FirstName"], section["LastName"],
            section.GetSection("Roles").Get<string[]>()));
    }

    return accounts
        .GroupBy(a => a.Email, StringComparer.OrdinalIgnoreCase)
        .Select(g => g.First())
        .ToList();
}

// --- HTTP pipeline --------------------------------------------------------------------------------
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");
    app.UseHsts();
}

app.UseHttpsRedirection();

app.UseRequestLocalization(app.Services.GetRequiredService<IOptions<RequestLocalizationOptions>>().Value);

app.UseRouting();

app.UseRateLimiter();

app.UseAuthentication();
app.UseAuthorization();

// The service worker script itself must never be aggressively cached by the browser's own HTTP
// cache — otherwise a stuck stale sw.js means updates to it (and its cache-versioning logic) never
// reach returning visitors. MapStaticAssets' fingerprinting is exactly wrong for this one file.
app.Use(async (context, next) =>
{
    if (context.Request.Path == "/sw.js")
    {
        // Set after next(): the static file middleware sets its own Cache-Control on the way out,
        // and setting ours beforehand left both present in the response (duplicate header) — this
        // way ours is the one that actually lands.
        context.Response.OnStarting(() =>
        {
            context.Response.Headers.CacheControl = "no-cache";
            return Task.CompletedTask;
        });
    }
    await next();
});

app.MapStaticAssets();

// The admin panel (Areas/Admin) is meant to live on its own subdomain, not be discoverable by
// crawling the public site (e.g. https://admin.thevihouse.com rather than thevihouse.com/Admin/...).
// AdminHost is unset in Development, so this route stays unrestricted on localhost — set it in
// appsettings.Production.json (see the "AdminHost" key) to gate it for real.
var adminHost = builder.Configuration["AdminHost"];
var adminRoute = app.MapControllerRoute(
    name: "areas",
    pattern: "{area:exists}/{controller=AdminDashboard}/{action=Index}/{id?}")
    .WithStaticAssets();
if (!string.IsNullOrWhiteSpace(adminHost))
{
    adminRoute.RequireHost(adminHost, "localhost:*", "127.0.0.1:*");
}

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}")
    .WithStaticAssets();

app.MapRazorPages()
    .WithStaticAssets();

app.Run();
