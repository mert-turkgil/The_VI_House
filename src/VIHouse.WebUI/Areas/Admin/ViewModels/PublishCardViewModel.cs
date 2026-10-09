namespace VIHouse.WebUI.Areas.Admin.ViewModels;

/// <summary>
/// The "Publishing" card at the top of an editor screen (Areas/Admin/Views/Shared/_PublishCard.cshtml):
/// where the item stands, who can see it, what is still missing, and the buttons that change it.
///
/// It exists because the status used to be a small badge beside a separate Publish button. Saving the
/// details of a draft then looked exactly like publishing it, and the session was never live. This
/// card says it in a sentence instead.
///
/// Two ways to drive the buttons:
///  - <see cref="StatusAction"/>: each button is its own form posting <c>status=…</c> there (sessions).
///  - <see cref="WriterFormId"/>: each button submits the writer's form with <c>intent=…</c>, so
///    publishing also saves what was just typed (journal posts).
/// </summary>
public class PublishCardViewModel
{
    public bool IsPublished { get; init; }
    public bool IsArchived { get; init; }

    /// <summary>"Draft", "Published", "Archived": already localised.</summary>
    public string StatusLabel { get; init; } = default!;

    /// <summary>The state as a sentence, e.g. "Draft: not visible on the website yet."</summary>
    public string StatusSentence { get; init; } = default!;

    /// <summary>Who can see it once it is live, as a sentence. Null when there is only one audience.</summary>
    public string? AudienceSentence { get; init; }

    /// <summary>True when the audience is narrower than "everyone", worth a second look.</summary>
    public bool AudienceWarning { get; init; }

    public List<PublishCheck> Checks { get; init; } = [];

    /// <summary>Publishing is allowed once every required check is done; the server enforces the same
    /// rule, this only says so before the click.</summary>
    public bool CanPublish => Checks.Where(c => c.Required).All(c => c.Done);

    /// <summary>The public page. Staff can open it at any status; it shows a preview banner.</summary>
    public string? PreviewUrl { get; init; }

    public string? StatusAction { get; init; }
    public bool AllowArchive { get; init; }
    public string? WriterFormId { get; init; }
}

/// <param name="Label">What is checked, e.g. "English description".</param>
/// <param name="Required">Blocks publishing when not done. Optional checks are advice.</param>
/// <param name="FixHref">Where to go to fix it (an anchor on the page or another tab).</param>
public record PublishCheck(string Label, bool Done, bool Required, string? FixHref = null);
