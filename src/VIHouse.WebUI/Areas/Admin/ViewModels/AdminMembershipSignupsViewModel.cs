using VIHouse.Entities.Membership;

namespace VIHouse.WebUI.Areas.Admin.ViewModels;

public class AdminMembershipSignupsViewModel
{
    public List<PendingJoin> Rows { get; set; } = [];
    public PendingJoinStatus? Status { get; set; }
    public IReadOnlyDictionary<Guid, string> PlanNames { get; set; } = new Dictionary<Guid, string>();

    public int Page { get; set; } = 1;
    public int PageSize { get; set; } = 50;
    public int TotalCount { get; set; }
    public int PaidCount { get; set; }

    public int TotalPages => TotalCount == 0 ? 1 : (int)Math.Ceiling(TotalCount / (double)PageSize);
    public bool HasPrevious => Page > 1;
    public bool HasNext => Page < TotalPages;
}

public class AdminMembershipSignupDetailViewModel
{
    public PendingJoin Join { get; set; } = default!;
    public string PlanName { get; set; } = "—";
    public string? PromoCode { get; set; }
    public Guid? AccountId { get; set; }
    public bool AccountEverSignedIn { get; set; }
}
