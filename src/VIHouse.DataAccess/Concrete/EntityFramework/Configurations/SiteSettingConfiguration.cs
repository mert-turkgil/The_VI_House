using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using VIHouse.Entities.Settings;

namespace VIHouse.DataAccess.Concrete.EntityFramework.Configurations;

public class SiteSettingConfiguration : IEntityTypeConfiguration<SiteSetting>
{
    public void Configure(EntityTypeBuilder<SiteSetting> builder)
    {
        builder.ToTable("SiteSettings");

        builder.Property(s => s.CanonicalBaseUrl).HasMaxLength(300);
        builder.Property(s => s.OrganizationType).HasMaxLength(50).IsRequired();
        builder.Property(s => s.LegalName).HasMaxLength(200);
        builder.Property(s => s.FoundingDate).HasMaxLength(20);
        builder.Property(s => s.LogoUrl).HasMaxLength(500);
        builder.Property(s => s.LogoStorageKey).HasMaxLength(400);
        builder.Property(s => s.DefaultOgImageUrl).HasMaxLength(500);
        builder.Property(s => s.DefaultOgImageStorageKey).HasMaxLength(400);

        builder.Property(s => s.InstagramUrl).HasMaxLength(500);
        builder.Property(s => s.LinkedInUrl).HasMaxLength(500);
        builder.Property(s => s.XUrl).HasMaxLength(500);
        builder.Property(s => s.FacebookUrl).HasMaxLength(500);
        builder.Property(s => s.YouTubeUrl).HasMaxLength(500);
        builder.Property(s => s.TikTokUrl).HasMaxLength(500);
        builder.Property(s => s.TwitterHandle).HasMaxLength(50);

        builder.Property(s => s.ContactEmail).HasMaxLength(320);
        builder.Property(s => s.ContactPhone).HasMaxLength(40);
        builder.Property(s => s.StreetAddress).HasMaxLength(300);
        builder.Property(s => s.AddressLocality).HasMaxLength(120);
        builder.Property(s => s.AddressRegion).HasMaxLength(120);
        builder.Property(s => s.PostalCode).HasMaxLength(20);
        builder.Property(s => s.AddressCountry).HasMaxLength(2);

        builder.Property(s => s.GoogleSiteVerification).HasMaxLength(200);
        builder.Property(s => s.BingSiteVerification).HasMaxLength(200);

        // RobotsExtra is unbounded on purpose: it is a block of directives, not a field.

        builder.HasMany(s => s.Translations)
            .WithOne()
            .HasForeignKey(t => t.SiteSettingId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}

public class SiteSettingTranslationConfiguration : IEntityTypeConfiguration<SiteSettingTranslation>
{
    public void Configure(EntityTypeBuilder<SiteSettingTranslation> builder)
    {
        builder.ToTable("SiteSettingTranslations");

        // One row per culture, for the reason every other translation table gives: a double
        // submitted "add German" otherwise leaves two German copies and the read side picks
        // whichever the database returns first.
        builder.HasIndex(t => new { t.SiteSettingId, t.Culture }).IsUnique();

        builder.Property(t => t.Culture).HasMaxLength(10).IsRequired();
        builder.Property(t => t.SiteName).HasMaxLength(120).IsRequired();
        builder.Property(t => t.TitleTemplate).HasMaxLength(120).IsRequired();
        builder.Property(t => t.HomeTitle).HasMaxLength(200);

        // 320 rather than 160: the search-result snippet truncates near 160, but the field is also
        // the og:description, where more is read. The admin screen shows a live character count
        // against the 160 that matters.
        builder.Property(t => t.DefaultMetaDescription).HasMaxLength(320);
        builder.Property(t => t.OrganizationDescription).HasMaxLength(500);
        builder.Property(t => t.OgImageAlt).HasMaxLength(300);
        builder.Property(t => t.OgImageUrl).HasMaxLength(500);
        builder.Property(t => t.OgImageStorageKey).HasMaxLength(400);
    }
}
