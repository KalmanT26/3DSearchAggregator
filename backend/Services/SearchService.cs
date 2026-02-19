using ModelAggregator.Api.Adapters;
using ModelAggregator.Api.DTOs;

namespace ModelAggregator.Api.Services;

/// <summary>
/// On-demand search service that queries all registered source adapters in parallel
/// and merges/aggregates the results.
/// </summary>
public class SearchService(IEnumerable<IModelSourceAdapter> adapters, ILogger<SearchService> logger)
{
    /// <summary>
    /// Search across all sources in parallel, merge and return paginated results.
    /// </summary>
    public async Task<SearchResponse> SearchAsync(SearchRequest request, CancellationToken ct = default)
    {
        var query = request.Query?.Trim() ?? "";
        if (string.IsNullOrEmpty(query))
        {
            return await GetTrendingAsync(request, ct);
        }

        // Filter adapters by requested sources
        var filteredAdapters = adapters.AsEnumerable();
        if (request.Sources?.Count > 0)
        {
            filteredAdapters = filteredAdapters.Where(a => request.Sources.Contains(a.Source.ToString(), StringComparer.OrdinalIgnoreCase));
        }

        // Query all sources in parallel
        var adapterCount = filteredAdapters.Count();
        var isRelevance = string.IsNullOrEmpty(request.SortBy) || request.SortBy == "relevance";

        // Fix for diminishing results: Request full page size from EACH source.
        // This ensures that if some sources return 0 items, we can still fill the page with items from other sources.
        // Trade-off: Some items from high-volume sources might be skipped in the interleaving process between pages,
        // but this guarantees full pages and consistency.
        var perSourcePageSize = request.PageSize;

        var searchTasks = filteredAdapters.Select(adapter =>
            SearchSourceSafeAsync(adapter, query, request.Page, perSourcePageSize, request.SortBy, ct)
        );

        var results = await Task.WhenAll(searchTasks);

        return MergeResults(results, request);
    }

    /// <summary>
    /// Get trending models from all sources for the landing page.
    /// </summary>
    public async Task<SearchResponse> GetTrendingAsync(SearchRequest? request = null, CancellationToken ct = default)
    {
        request ??= new SearchRequest { Page = 1, PageSize = 24 };

        var filteredAdapters = adapters.AsEnumerable();
        if (request.Sources?.Count > 0)
        {
            filteredAdapters = filteredAdapters.Where(a => request.Sources.Contains(a.Source.ToString(), StringComparer.OrdinalIgnoreCase));
        }

        // Get trending models per source, with items evenly distributed
        var adapterCount = filteredAdapters.Count();
        var perSource = adapterCount > 0 ? Math.Max(8, request.PageSize / adapterCount) : request.PageSize;

        var trendingTasks = filteredAdapters.Select(adapter =>
            GetTrendingSafeAsync(adapter, request.Page, perSource, ct)
        );

        var results = await Task.WhenAll(trendingTasks);

        return MergeResults(results, request);
    }

    private async Task<AdapterSearchResult> SearchSourceSafeAsync(
        IModelSourceAdapter adapter, string query, int page, int pageSize, string? sort, CancellationToken ct)
    {
        try
        {
            var result = await adapter.SearchAsync(query, page, pageSize, sort, ct);
            logger.LogInformation("Search: Source {Source} returned {Count} models. Total: {Total}", 
                adapter.Source, result.Items.Count, result.TotalCount);
            return result;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Search failed for source {Source}: {Message}", adapter.Source, ex.Message);
            return new AdapterSearchResult { Source = adapter.Source.ToString() };
        }
    }

    private async Task<AdapterSearchResult> GetTrendingSafeAsync(
        IModelSourceAdapter adapter, int page, int pageSize, CancellationToken ct)
    {
        try
        {
            var result = await adapter.GetTrendingAsync(page, pageSize, ct);
            logger.LogInformation("Trending: Source {Source} returned {Count} models.", 
                adapter.Source, result.Items.Count);
            return result;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Trending fetch failed for source {Source}: {Message}", adapter.Source, ex.Message);
            return new AdapterSearchResult { Source = adapter.Source.ToString() };
        }
    }

    public async Task<ModelDto?> GetModelDetailsAsync(string source, string externalId, CancellationToken ct = default)
    {
        var adapter = adapters.FirstOrDefault(a => a.Source.ToString().Equals(source, StringComparison.OrdinalIgnoreCase));
        if (adapter == null)
        {
            logger.LogWarning("Requested details for unknown source: {Source}", source);
            return null;
        }

        try
        {
            return await adapter.GetModelDetailsAsync(externalId, ct);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to get details from {Source} for {Id}", source, externalId);
            return null;
        }
    }

    private SearchResponse MergeResults(AdapterSearchResult[] results, SearchRequest request)
    {
        // Merge all items from all sources
        var allItems = results.SelectMany(r => r.Items).ToList();

        // Apply client-side filters
        if (request.FreeOnly == true)
            allItems = [.. allItems.Where(m => m.IsFree)];

        if (request.MinPrice.HasValue)
            allItems = [.. allItems.Where(m => m.Price >= request.MinPrice.Value)];

        if (request.MaxPrice.HasValue)
            allItems = [.. allItems.Where(m => m.Price <= request.MaxPrice.Value)];

        // Sort merged results
        if (string.IsNullOrEmpty(request.SortBy) || request.SortBy == "relevance")
        {
            // Smart mixing: Interleave results from different sources (Round Robin)
            // This ensures the first page shows a balanced mix of sources.
            allItems = InterleaveBySource(allItems);
        }
        else
        {
            allItems = request.SortBy switch
            {
                "newest" => [.. allItems.OrderByDescending(m => m.CreatedAtSource)],

                "likes" => [.. allItems.OrderByDescending(m => m.LikeCount)],
                "price_asc" => [.. allItems.OrderBy(m => m.Price)],
                "price_desc" => [.. allItems.OrderByDescending(m => m.Price)],
                _ => allItems 
            };
        }

        var totalCount = results.Sum(r => r.TotalCount);
        var totalPages = totalCount > 0 ? (int)Math.Ceiling((double)totalCount / request.PageSize) : 1;

        // Apply pagination slicing (Take only, skip is already handled by adapters)
        var pagedItems = allItems
            .Take(request.PageSize)
            .ToList();

        return new SearchResponse
        {
            Results = pagedItems,
            TotalCount = totalCount,
            Page = request.Page,
            PageSize = request.PageSize,
            TotalPages = totalPages
        };
    }

    private List<ModelDto> InterleaveBySource(List<ModelDto> items)
    {
        if (items.Count == 0) return items;

        var sourceGroups = items
            .GroupBy(m => m.Source)
            .Select(g => new { Source = g.Key, Queue = new Queue<ModelDto>(g) })
            .ToList();

        logger.LogInformation("Interleaving {Count} items from {SourceCount} sources: {Sources}", 
            items.Count, sourceGroups.Count, string.Join(", ", sourceGroups.Select(g => $"{g.Source}({g.Queue.Count})")));
        
        var interleaved = new List<ModelDto>();
        while (sourceGroups.Any(q => q.Queue.Count > 0))
        {
            foreach (var group in sourceGroups)
            {
                if (group.Queue.Count > 0)
                {
                    interleaved.Add(group.Queue.Dequeue());
                }
            }
        }

        return interleaved;
    }
}
