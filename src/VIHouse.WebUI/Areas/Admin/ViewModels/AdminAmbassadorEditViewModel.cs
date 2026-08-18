using System.ComponentModel.DataAnnotations;
using VIHouse.Business.Abstract;
using VIHouse.Entities.Referrals;

namespace VIHouse.WebUI.Areas.Admin.ViewModels;

public class AdminAmbassadorEditViewModel
{
    public Guid Id { get; set; }
    public string Code { get; set; } = default!;
    public string? Email { get; set; }

    [Required, StringLength(150)]
    public string Name { get; set; } = default!;

    [Required, Range(0, 100)]
    public decimal CommissionPercent { get; set; }

    [Required]
    public AmbassadorStatus Status { get; set; }

    public AmbassadorStats? Stats { get; set; }

    public static AdminAmbassadorEditViewModel FromEntity(Ambassador a, string? email) => new()
    {
        Id = a.Id,
        Code = a.Code,
        Email = email,
        Name = a.Name,
        CommissionPercent = a.CommissionPercent,
        Status = a.Status,
    };

    public Ambassador ToEntity() => new()
    {
        Id = Id,
        Code = Code,
        Name = Name.Trim(),
        CommissionPercent = CommissionPercent,
        Status = Status,
    };
}
