using System.Globalization;
using Microsoft.AspNetCore.Identity;
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

// English-only for Phase 1 (brief §66) — pinned explicitly so date/number formatting (e.g.
// experience card dates) doesn't silently follow whatever OS locale the app happens to run under.
var defaultCulture = new CultureInfo("en-GB");
CultureInfo.DefaultThreadCurrentCulture = defaultCulture;
CultureInfo.DefaultThreadCurrentUICulture = defaultCulture;

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

// Only set when the admin panel lives on its own subdomain (see "AdminHost" below) — a shared
// cookie domain (e.g. ".thevihouse.com") lets one login work across both thevihouse.com and
// admin.thevihouse.com. Left unset in Development, where everything is on localhost anyway.
var cookieDomain = builder.Configuration["CookieDomain"];
if (!string.IsNullOrWhiteSpace(cookieDomain))
{
    builder.Services.ConfigureApplicationCookie(options => options.Cookie.Domain = cookieDomain);
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

// --- Business services -------------------------------------------------------------------------
builder.Services.AddScoped<IExperienceService, ExperienceService>();
builder.Services.AddScoped<IApplicationService, ApplicationService>();
builder.Services.AddScoped<ICapacityService, CapacityService>();
builder.Services.AddScoped<IPaymentService, PaymentService>();

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

// --- MVC + Razor Pages (Identity UI is Razor-Pages-based) -----------------------------------------
builder.Services.AddControllersWithViews(options =>
{
    // Global, but self-scoping: the filter only acts on requests routed into the Admin area
    // (brief §95 — 2FA for admins). See AdminTwoFactorRequirementFilter for the check itself.
    options.Filters.Add(typeof(AdminTwoFactorRequirementFilter));
});
builder.Services.AddRazorPages();

var app = builder.Build();

// Fail fast in Production rather than silently taking money-facing requests with no Stripe keys
// configured — Development is allowed to run without them (only checkout/webhook routes need them).
if (app.Environment.IsProduction())
{
    var stripeOptions = app.Services.GetRequiredService<IOptions<StripeOptions>>().Value;
    if (string.IsNullOrWhiteSpace(stripeOptions.SecretKey) || string.IsNullOrWhiteSpace(stripeOptions.WebhookSecret))
    {
        throw new InvalidOperationException(
            "Stripe:SecretKey / Stripe:WebhookSecret are not configured. Set them via environment " +
            "variables (Stripe__SecretKey, Stripe__WebhookSecret) or your secrets manager — never in " +
            "appsettings.Production.json.");
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
app.UseRouting();

app.UseAuthentication();
app.UseAuthorization();

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
