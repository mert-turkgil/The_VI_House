using VIHouse.Entities.Experiences;

namespace VIHouse.WebUI.Helpers;

public static class ExperienceStatusFormatter
{
    public static string ToDisplayLabel(this ExperienceStatus status) => status switch
    {
        ExperienceStatus.ApplicationsOpen => "Applications Open",
        ExperienceStatus.AlmostFull => "Almost Full",
        ExperienceStatus.ApplicationsClosed => "Applications Closed",
        ExperienceStatus.ComingSoon => "Coming Soon",
        _ => status.ToString(),
    };
}
