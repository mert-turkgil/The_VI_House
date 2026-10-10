using Microsoft.Extensions.Configuration.Json;
using Microsoft.Extensions.Configuration.Memory;

namespace VIHouse.WebUI.Helpers;

/// <summary>
/// What a fresh clone needs to start in Development with no secrets at all: a LocalDB database, a
/// local admin, two-step verification and the launch curtain off, and mail pointed at a local
/// catcher (smtp4dev / Papercut on port 25). Nothing here is secret; the admin exists only in the
/// contributor's own LocalDB.
///
/// Inserted directly after appsettings.json, so appsettings.Development.json, user-secrets and
/// environment variables all still win — a maintainer's real setup is untouched. Never applied
/// outside Development.
/// </summary>
public static class DevelopmentDefaults
{
    public const string AdminEmail = "admin@vihouse.local";
    public const string AdminPassword = "VIHouse-Dev-Admin-1!";

    private static readonly Dictionary<string, string?> Values = new()
    {
        ["ConnectionStrings:DefaultConnection"] =
            @"Server=(localdb)\MSSQLLocalDB;Database=VIHouse_Dev;Trusted_Connection=True;MultipleActiveResultSets=true;TrustServerCertificate=True",
        ["Features:RequireTwoFactor"] = "false",
        ["Features:ComingSoon"] = "false",
        ["SeedAdmin:Email"] = AdminEmail,
        ["SeedAdmin:Password"] = AdminPassword,
        ["Smtp:Host"] = "localhost",
        ["Smtp:Port"] = "25",
        ["Smtp:UseSsl"] = "false",
        ["Smtp:FromEmail"] = "noreply@vihouse.local",
    };

    public static void Apply(WebApplicationBuilder builder)
    {
        if (!builder.Environment.IsDevelopment()) return;

        var sources = builder.Configuration.Sources;
        var baseFile = sources.ToList().FindIndex(s => s is JsonConfigurationSource { Path: "appsettings.json" });
        sources.Insert(baseFile + 1, new MemoryConfigurationSource { InitialData = Values });
    }
}
