namespace VIHouse.Entities.Seminars;

/// <summary>
/// Who may reach a published seminar's page. Distinct from who may see its *content* — the body and
/// media are behind enrolment regardless, and this only governs the page itself.
/// </summary>
public enum SeminarVisibility
{
    /// <summary>Listed publicly; anyone can read the summary and see the price.</summary>
    Public,

    /// <summary>The "private" setting: listed and reachable only for signed-in members. Everyone
    /// else gets a 404 rather than a login prompt, so its existence is not advertised.</summary>
    Members,

    /// <summary>Reachable by direct link but absent from every listing — for a session shared by
    /// invitation without being announced.</summary>
    Unlisted,
}
