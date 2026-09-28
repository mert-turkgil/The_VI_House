namespace VIHouse.Entities.Common;

/// <summary>A per-language row of some content (experience, session, journal post, hero slide,
/// content block). Lets one lookup (TranslationLookup) serve all of them.</summary>
public interface ITranslation
{
    /// <summary>"en-GB", "de-DE", "tr-TR", "et-EE".</summary>
    string Culture { get; }
}
