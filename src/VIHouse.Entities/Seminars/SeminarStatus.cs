namespace VIHouse.Entities.Seminars;

public enum SeminarStatus
{
    /// <summary>Being written. Reachable only from the admin panel, at any visibility.</summary>
    Draft,

    /// <summary>Live, subject to <see cref="SeminarVisibility"/>.</summary>
    Published,

    /// <summary>Retired from the listings. Existing enrolments keep working, so someone who paid
    /// for it does not lose access the day it stops being offered.</summary>
    Archived,
}
