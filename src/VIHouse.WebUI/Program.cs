using System.Globalization;
using System.Threading.RateLimiting;
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
using VIHouse.WebUI.Areas.Admin.Filters;
using VIHouse.WebUI.Services;

// Process-wide fallback only (background services like TicketHoldExpiryService run with no HTTP
// request, so there's no per-request culture to fall back to) — the real per-request culture for
// site chrome/static pages is chosen by UseRequestLocalization below, driven by a cookie set via
// CultureController (brief: EN/DE/TR/ET). Admin area and CMS/experience content stay en-GB only.
var defaultCulture = new CultureInfo("en-GB");
CultureInfo.DefaultThreadCurrentCulture = defaultCulture;
CultureInfo.DefaultThreadCurrentUICulture = defaultCulture;

var supportedCultures = new[] { "en-GB", "de-DE", "tr-TR", "et-EE" };

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
var cookieDomain = builder.Configuration["CookieDomain"];
if (!string.IsNullOrWhiteSpace(cookieDomain))
{
    builder.Services.ConfigureApplicationCookie(options => options.Cookie.Domain = cookieDomain);
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
    options.SetDefaultCulture(supportedCultures[0]);
    options.AddSupportedCultures(supportedCultures);
    options.AddSupportedUICultures(supportedCultures);
    options.RequestCultureProviders = [new CookieRequestCultureProvider { CookieName = CookieRequestCultureProvider.DefaultCookieName }];
});

// --- MVC + Razor Pages (Identity UI is Razor-Pages-based) -----------------------------------------
builder.Services.AddControllersWithViews(options =>
{
    // Global, but self-scoping: the filter only acts on requests routed into the Admin area
    // (brief §95 — 2FA for admins). See AdminTwoFactorRequirementFilter for the check itself.
    options.Filters.Add(typeof(AdminTwoFactorRequirementFilter));

    // Defense-in-depth: every POST/PUT/DELETE/PATCH is antiforgery-checked by default now, not
    // just the ones that remembered to add [ValidateAntiForgeryToken]. The one deliberate
    // exception is the Stripe webhook, which opts out explicitly via [IgnoreAntiforgeryToken]
    // (it's a server-to-server call with no browser cookie/form to carry a token).
    options.Filters.Add(new AutoValidateAntiforgeryTokenAttribute());
});
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

// --- Development-only: apply migrations + seed data --------------------------------------------
if (app.Environment.IsDevelopment())
{
    using var scope = app.Services.CreateScope();
    var db = scope.ServiceProvider.GetRequiredService<VIHouseDbContext>();
    var userManager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
    var roleManager = scope.ServiceProvider.GetRequiredService<RoleManager<ApplicationRole>>();

    var seedAdminEmail = builder.Configuration["SeedAdmin:Email"]
        ?? throw new InvalidOperationException("SeedAdmin:Email is not configured.");
    var seedAdminPassword = builder.Configuration["SeedAdmin:Password"]
        ?? throw new InvalidOperationException("SeedAdmin:Password is not configured.");

    await DbSeeder.SeedAsync(db, userManager, roleManager, seedAdminEmail, seedAdminPassword);
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
