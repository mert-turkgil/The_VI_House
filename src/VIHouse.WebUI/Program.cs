using System.Globalization;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using VIHouse.Business.Abstract;
using VIHouse.Business.Concrete;
using VIHouse.DataAccess.Abstract;
using VIHouse.DataAccess.Concrete.EntityFramework;
using VIHouse.DataAccess.Concrete.EntityFramework.Seed;
using VIHouse.DataAccess.Identity;
using VIHouse.WebUI.Areas.Admin.Filters;

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

// --- MVC + Razor Pages (Identity UI is Razor-Pages-based) -----------------------------------------
builder.Services.AddControllersWithViews(options =>
{
    // Global, but self-scoping: the filter only acts on requests routed into the Admin area
    // (brief §95 — 2FA for admins). See AdminTwoFactorRequirementFilter for the check itself.
    options.Filters.Add(typeof(AdminTwoFactorRequirementFilter));
});
builder.Services.AddRazorPages();

var app = builder.Build();

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

app.MapControllerRoute(
    name: "areas",
    pattern: "{area:exists}/{controller=AdminDashboard}/{action=Index}/{id?}")
    .WithStaticAssets();

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}")
    .WithStaticAssets();

app.MapRazorPages()
    .WithStaticAssets();

app.Run();
