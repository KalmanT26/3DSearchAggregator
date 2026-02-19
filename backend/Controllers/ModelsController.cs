using Microsoft.AspNetCore.Mvc;
using ModelAggregator.Api.DTOs;
using ModelAggregator.Api.Services;

namespace ModelAggregator.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class ModelsController(SearchService searchService, IRandomSearchService randomService) : ControllerBase
{
    private static readonly string[] DefaultSources = ["Thingiverse", "Cults3D", "MyMiniFactory", "Printables", "MakerWorld"];

    /// <summary>
    /// Search for 3D models across all sources in real-time.
    /// If no query is provided, returns trending/popular models.
    /// </summary>
    [HttpGet("search")]
    public async Task<ActionResult<SearchResponse>> Search(
        [FromQuery] string? q,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 24,
        [FromQuery] string? sortBy = "relevance",
        [FromQuery] string? sources = null,
        [FromQuery] bool? freeOnly = null,
        [FromQuery] decimal? minPrice = null,
        [FromQuery] decimal? maxPrice = null,
        CancellationToken ct = default)
    {
        var request = new SearchRequest
        {
            Query = q ?? "",
            Page = Math.Max(1, page),
            PageSize = Math.Clamp(pageSize, 1, 100),
            SortBy = sortBy,
            Sources = sources?.Split(',', StringSplitOptions.RemoveEmptyEntries).ToList(),
            FreeOnly = freeOnly,
            MinPrice = minPrice,
            MaxPrice = maxPrice
        };

        var result = await searchService.SearchAsync(request, ct);
        return Ok(result);
    }

    /// <summary>
    /// Get trending/popular models for the landing page.
    /// </summary>
    [HttpGet("trending")]
    public async Task<ActionResult<SearchResponse>> GetTrending(
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 24,
        [FromQuery] string? sources = null,
        CancellationToken ct = default)
    {
        var request = new SearchRequest
        {
            Page = Math.Max(1, page),
            PageSize = Math.Clamp(pageSize, 1, 100),
            Sources = sources?.Split(',', StringSplitOptions.RemoveEmptyEntries).ToList(),
        };

        var result = await searchService.GetTrendingAsync(request, ct);
        randomService.IngestTrendingTags(result.Results);
        return Ok(result);
    }

    /// <summary>
    /// Get available filter options (sources, categories, etc.)
    /// </summary>
    [HttpGet("filters")]
    public IActionResult GetFilters()
    {
        return Ok(new
        {
            Sources = DefaultSources,
            SortOptions = new[]
            {
                new { Value = "relevance", Label = "Relevance" },
                new { Value = "newest", Label = "Newest" },
                new { Value = "popular", Label = "Most Downloaded" },
                new { Value = "likes", Label = "Most Liked" },
                new { Value = "price_asc", Label = "Price: Low to High" },
                new { Value = "price_desc", Label = "Price: High to Low" }
            }
        });
    }

    /// <summary>
    /// Get full details for a specific model from a specific source.
    /// </summary>
    [HttpGet("{source}/{id}")]
    public async Task<ActionResult<ModelDto>> GetModelDetails(
        string source,
        string id,
        CancellationToken ct = default)
    {
        // Decode ID if it was URL encoded by frontend (though path params usually decoded by default)
        var decodedId = Uri.UnescapeDataString(id); 
        var result = await searchService.GetModelDetailsAsync(source, decodedId, ct);
        
        if (result == null) 
            return NotFound($"Model {id} not found on {source}");

        return Ok(result);
    }
    [HttpGet("random-term")]
    public async Task<ActionResult<ModelDto>> GetRandomTerm(CancellationToken ct = default)
    {
        var term = await randomService.GetRandomTermAsync(ct);
        return Ok(new { term });
    }
}
