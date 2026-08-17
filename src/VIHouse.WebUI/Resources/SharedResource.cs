namespace VIHouse.WebUI;

/// <summary>
/// Marker type only — anchors IStringLocalizer&lt;SharedResource&gt; to Resources/SharedResource*.resx.
/// Deliberately declared in the root namespace (not VIHouse.WebUI.Resources) so the resx lookup
/// convention resolves directly to Resources/SharedResource.resx without an extra nested folder.
/// </summary>
public class SharedResource;
