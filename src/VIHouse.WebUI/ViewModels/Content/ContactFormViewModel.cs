using System.ComponentModel.DataAnnotations;

namespace VIHouse.WebUI.ViewModels.Content;

public class ContactFormViewModel
{
    [Required, StringLength(150)]
    public string Name { get; set; } = default!;

    [Required, EmailAddress, StringLength(320)]
    public string Email { get; set; } = default!;

    [StringLength(150)]
    public string? Subject { get; set; }

    [Required, StringLength(4000)]
    public string Message { get; set; } = default!;
}
