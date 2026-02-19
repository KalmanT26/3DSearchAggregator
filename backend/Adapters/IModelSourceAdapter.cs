using ModelAggregator.Api.DTOs;

namespace ModelAggregator.Api.Adapters;

/// <summary>
/// Interface for on-demand search against 3D model platforms.
/// </summary>
public interface IModelSourceAdapter
{
    Models.ModelSource Source { get; }

    /// <summary>
    /// Search for models using a query string.
    /// </summary>
    Task<AdapterSearchResult> SearchAsync(string query, int page = 1, int pageSize = 20, string? sort = null, CancellationToken ct = default);

    /// <summary>
    /// Get trending/popular models from the source.
    /// </summary>
    Task<AdapterSearchResult> GetTrendingAsync(int page = 1, int pageSize = 20, CancellationToken ct = default);

    /// <summary>
    /// Fetch full details for a specific model.
    /// </summary>
    Task<ModelDto?> GetModelDetailsAsync(string externalId, CancellationToken ct = default);
}
