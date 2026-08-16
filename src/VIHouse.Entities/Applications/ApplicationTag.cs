using VIHouse.Entities.Common;

namespace VIHouse.Entities.Applications;

/// <summary>Admin-assigned CRM tag (e.g. "Investor", "Fintech", "VIP") — brief §112.</summary>
public class ApplicationTag : BaseEntity
{
    public Guid ApplicationId { get; set; }
    public string Label { get; set; } = default!;
}
