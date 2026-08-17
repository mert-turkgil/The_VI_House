namespace VIHouse.WebUI.ViewModels.Content;

/// <summary>TitleKey/BodyKey point at SharedResource entries — the body is one localized string with "\n\n"-separated paragraphs.</summary>
public class LegalDocumentViewModel
{
    public string TitleKey { get; set; } = default!;
    public string BodyKey { get; set; } = default!;
}
