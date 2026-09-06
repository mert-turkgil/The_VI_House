using VIHouse.Business.Options;

namespace VIHouse.WebUI.Areas.Admin.ViewModels;

/// <summary>The Translations screen: every key, every language, in one table.</summary>
public class AdminTranslationsViewModel
{
    public List<TranslationRowViewModel> Rows { get; set; } = [];

    /// <summary>In site order, English first — the columns of the table.</summary>
    public IReadOnlyList<SiteCulture> Cultures { get; set; } = [];

    /// <summary>The first segment of a key, before the dot: Admin, Onboarding, Nav … 22 of them.
    /// This is the analogue of "resource file" on a project with more than one.</summary>
    public List<string> Sections { get; set; } = [];

    public string? Section { get; set; }
    public string? Query { get; set; }
    public bool UntranslatedOnly { get; set; }

    public int TotalKeys { get; set; }
    public int MatchedKeys => Rows.Count;

    /// <summary>
    /// Values still identical to the English, across every language. Not "missing" — all four files
    /// are complete — so a missing count would read zero forever and teach nobody anything. This is
    /// the number that is actually true, and some of it is legitimate (Administrator, Community).
    /// </summary>
    public int NeedsAttention { get; set; }

    /// <summary>False when the .resx files are not on disk. The screen then renders read-only and
    /// says why, rather than accepting edits it would silently drop.</summary>
    public bool IsEditable { get; set; }

    public string ResourcesPath { get; set; } = "";
}

public class TranslationRowViewModel
{
    public string Key { get; set; } = "";
    public string Section { get; set; } = "";

    /// <summary>Culture name (en-GB, de-DE …) to the value in that language.</summary>
    public Dictionary<string, string> Values { get; set; } = [];

    /// <summary>At least one translation is still byte-identical to the English.</summary>
    public bool NeedsAttention { get; set; }

    /// <summary>The English value uses {0}-style holes, so every translation must keep them.
    /// Surfaced in the UI so a translator knows before they start typing.</summary>
    public bool HasPlaceholders { get; set; }
}
