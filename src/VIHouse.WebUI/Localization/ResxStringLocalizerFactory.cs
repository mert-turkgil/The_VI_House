using Microsoft.Extensions.Localization;

namespace VIHouse.WebUI.Localization;

/// <summary>
/// Hands out <see cref="ResxStringLocalizer"/> when the .resx files are on disk, and defers to the
/// framework's resource-manager factory when they are not.
///
/// That fallback is the safety net for the whole feature. The .resx files are build inputs, so a
/// published app has them only because the csproj copies them; if that ever stops being true, or
/// the configured path is wrong, the site keeps rendering from the compiled satellite assemblies
/// exactly as it did before any of this existed — rather than showing raw key names on every page.
///
/// Anything that is not SharedResource is passed straight through, so this only ever intercepts the
/// one resource family the site actually has.
/// </summary>
public sealed class ResxStringLocalizerFactory(
    ResxCatalog catalog,
    ResourceManagerStringLocalizerFactory fallback,
    ILoggerFactory loggerFactory) : IStringLocalizerFactory
{
    private readonly Lazy<ResxStringLocalizer> _localizer =
        new(() => new ResxStringLocalizer(catalog, loggerFactory.CreateLogger<ResxStringLocalizer>()));

    public IStringLocalizer Create(Type resourceSource) =>
        catalog.IsAvailable && resourceSource == typeof(SharedResource)
            ? _localizer.Value
            : fallback.Create(resourceSource);

    public IStringLocalizer Create(string baseName, string location) =>
        catalog.IsAvailable && baseName.EndsWith(nameof(SharedResource), StringComparison.Ordinal)
            ? _localizer.Value
            : fallback.Create(baseName, location);
}
