using System.ComponentModel.DataAnnotations;
using VIHouse.Entities.Notifications;

namespace VIHouse.WebUI.Areas.Admin.ViewModels;

public class NotifyAttendeesInputModel
{
    public Guid ExperienceId { get; set; }

    [Required]
    public NotificationType Type { get; set; } = NotificationType.EventUpdate;

    [Required, StringLength(200)]
    public string Title { get; set; } = default!;

    [Required, StringLength(1000)]
    public string Body { get; set; } = default!;
}
