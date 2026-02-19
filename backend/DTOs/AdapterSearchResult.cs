namespace ModelAggregator.Api.DTOs;

public class AdapterSearchResult
{
    public string Source { get; set; } = "";
    public int TotalCount { get; set; }
    public List<ModelDto> Items { get; set; } = new();
}
