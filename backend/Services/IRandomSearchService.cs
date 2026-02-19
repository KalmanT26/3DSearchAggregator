using ModelAggregator.Api.DTOs;

namespace ModelAggregator.Api.Services;

public interface IRandomSearchService
{
    Task<string> GetRandomTermAsync(CancellationToken ct = default);
    void IngestTrendingTags(IEnumerable<ModelDto> models);
    Task RefreshDynamicTagsAsync(CancellationToken ct = default);
}
