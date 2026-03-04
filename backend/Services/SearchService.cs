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
    /// Returns true when the user picked a sort that requires a single, unified ordering
    /// across all sources (likes, newest, price). "relevance" is excluded because each
    /// source defines relevance differently and we interleave those results instead.
    /// </summary>
    private static bool IsGlobalSort(string? sortBy)
        => sortBy is "likes" or "newest" or "price_asc" or "price_desc" or "popular";

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

        // --- Pagination strategy depends on sort mode ---
        // For GLOBAL sorts (likes, newest, price) we need a correct cross-source ordering.
        // We can't rely on each adapter's own page N because source A's page-2 item can
        // have more likes than source B's page-1 item. So we ask each source for a large
        // enough window (page × pageSize items starting from page 1), merge everything,
        // sort globally, then skip/take for the requested page.
        //
        // For RELEVANCE we keep the lightweight per-source-page approach + interleaving,
        // since there is no meaningful global relevance metric.
        var globalSort = IsGlobalSort(request.SortBy);

        // How many items to request from each source
        var perSourcePageSize = globalSort
            ? request.Page * request.PageSize   // large window so we can paginate within the merged set
            : request.PageSize;                 // one page per source is enough for relevance

        // Which page to request from the adapter
        var adapterPage = globalSort ? 1 : request.Page;

        var searchTasks = filteredAdapters.Select(adapter =>
            SearchSourceSafeAsync(adapter, query, adapterPage, perSourcePageSize, request.SortBy, ct)
        );

        var results = await Task.WhenAll(searchTasks);

        return MergeResults(results, request, globalSort);
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

        return MergeResults(results, request, false);
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

    private SearchResponse MergeResults(AdapterSearchResult[] results, SearchRequest request, bool globalSort)
    {
        // 1. Convert each adapter's results into a queue, applying client-side filters
        var filteredSourceQueues = results
            .Select(r =>
            {
                var validItems = r.Items.Where(m =>
                {
                    if (request.FreeOnly == true && !m.IsFree) return false;
                    if (request.MinPrice.HasValue && m.Price < request.MinPrice.Value) return false;
                    if (request.MaxPrice.HasValue && m.Price > request.MaxPrice.Value) return false;
                    return true;
                }).ToList();

                return new { Source = r.Source, Queue = new Queue<ModelDto>(validItems), OriginalCount = r.TotalCount };
            })
            .Where(q => q.Queue.Count > 0)
            .ToList();

        var mergedItems = new List<ModelDto>();
        var totalCount = results.Sum(r => r.TotalCount);

        if (!globalSort)
        {
            // Smart mixing: Interleave results from different sources (Round Robin)
            while (filteredSourceQueues.Any(q => q.Queue.Count > 0))
            {
                foreach (var group in filteredSourceQueues)
                {
                    if (group.Queue.Count > 0)
                    {
                        mergedItems.Add(group.Queue.Dequeue());
                    }
                }
            }
        }
        else
        {
            // N-way Streaming Merge for Global Sorts
            // Instead of throwing all items into one bucket and running OrderBy() (which breaks
            // when APIs don't return strictly monotonic liked/priced items), we stream the top
            // item off the queue of whichever adapter currently has the "best" item.
            var sortBy = request.SortBy ?? "newest";

            while (filteredSourceQueues.Any(q => q.Queue.Count > 0))
            {
                // Find the queue whose first element is the "best" according to the sort criteria
                var bestQueue = filteredSourceQueues
                    .Where(q => q.Queue.Count > 0)
                    .Select(q => q) // Identity
                    .Aggregate((currentBest, next) =>
                    {
                        var bestItem = currentBest.Queue.Peek();
                        var nextItem = next.Queue.Peek();

                        bool nextIsBetter = sortBy switch
                        {
                            "newest" => nextItem.CreatedAtSource > bestItem.CreatedAtSource,
                            "popular" => nextItem.LikeCount > bestItem.LikeCount,
                            "likes" => nextItem.LikeCount > bestItem.LikeCount,
                            "price_asc" => nextItem.Price < bestItem.Price,
                            "price_desc" => nextItem.Price > bestItem.Price,
                            _ => false // Default fallback
                        };

                        return nextIsBetter ? next : currentBest;
                    });

                // Dequeue the best item and add it to the merged stream
                mergedItems.Add(bestQueue.Queue.Dequeue());
            }
        }

        var totalPages = totalCount > 0 ? (int)Math.Ceiling((double)totalCount / request.PageSize) : 1;

        // Skip to the correct page
        var skip = globalSort ? (request.Page - 1) * request.PageSize : 0;
        var pagedItems = mergedItems
            .Skip(skip)
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
