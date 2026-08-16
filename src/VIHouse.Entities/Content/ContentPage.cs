using VIHouse.Entities.Common;

namespace VIHouse.Entities.Content;

/// <summary>
/// Admin-editable page (starting with "home") made of ordered ContentBlocks — so homepage
/// copy/images/stats can change without a code deploy (brief §60).
/// </summary>
public class ContentPage : BaseEntity
{
    public string Slug { get; set; } = default!;
    public string Title { get; set; } = default!;
    public string? MetaDescription { get; set; }
    public string? OgImageUrl { get; set; }
    public bool IsPublished { get; set; } = true;
    public Guid? UpdatedByUserId { get; set; }

    public List<ContentBlock> Blocks { get; set; } = [];
}
