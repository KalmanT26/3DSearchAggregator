namespace ModelAggregator.Api.DTOs;

public class SearchRequest
{
    public string Query { get; set; } = "";
    public int Page { get; set; } = 1;
    public int PageSize { get; set; } = 24;
    public string? SortBy { get; set; } = "relevance"; // relevance, newest, popular, price_asc, price_desc
    public List<string>? Sources { get; set; }  // filter by source platforms
    public bool? FreeOnly { get; set; }
    public decimal? MinPrice { get; set; }
    public decimal? MaxPrice { get; set; }
}

public class SearchResponse
{
    public List<ModelDto> Results { get; set; } = new();
    public int TotalCount { get; set; }
    public int Page { get; set; }
    public int PageSize { get; set; }
    public int TotalPages { get; set; }
}

public class ModelDto
{
    public int Id { get; set; }
    public string ExternalId { get; set; } = "";
    public string Source { get; set; } = "";
    public string SourceUrl { get; set; } = "";
    public string Title { get; set; } = "";
    public string Description { get; set; } = "";
    public List<string> Tags { get; set; } = new();
    public string ThumbnailUrl { get; set; } = "";
    public List<string> ImageUrls { get; set; } = new();
    public string CreatorName { get; set; } = "";
    public string CreatorProfileUrl { get; set; } = "";
    public decimal Price { get; set; }
    public string Currency { get; set; } = "";
    public bool IsFree { get; set; }
    public bool IsSubscriptionGated { get; set; }

    public int LikeCount { get; set; }
    public int ViewCount { get; set; }
    public int MakeCount { get; set; }
    public int FileCount { get; set; }

    public string? DescriptionHtml { get; set; }
    public string? License { get; set; }
    public string? Category { get; set; }
    public DateTime CreatedAtSource { get; set; }
}
