namespace VIHouse.WebUI.ViewModels.Search;

public class SearchResultsViewModel
{
    public string Query { get; set; } = "";
    public List<SearchResultItemViewModel> Results { get; set; } = [];
}
