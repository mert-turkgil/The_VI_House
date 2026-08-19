namespace VIHouse.WebUI.ViewModels.Search;

public class SearchResultItemViewModel
{
    public string Type { get; set; } = default!;
    public string Title { get; set; } = default!;
    public string? Subtitle { get; set; }
    public string Url { get; set; } = default!;
}
