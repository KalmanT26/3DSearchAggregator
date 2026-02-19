using System.Net;
using System.Text.Json;
using System.Text.RegularExpressions;
using ModelAggregator.Api.DTOs;
using ModelAggregator.Api.Models;

namespace ModelAggregator.Api.Adapters;

/// <summary>
/// Adapter for MakerWorld (Bambu Lab) using the Next.js _next/data SSR endpoint.
/// Uses a standalone HttpClient with proper browser headers to bypass Cloudflare,
/// since the DI-configured IHttpClientFactory client gets blocked.
/// </summary>
public partial class MakerWorldAdapter : IModelSourceAdapter
{
    private readonly ILogger<MakerWorldAdapter> _logger;
    private const string BaseUrl = "https://makerworld.com";
    private const string SiteBaseUrl = "https://makerworld.com/en/models/";

    // Cached buildId (changes on each MakerWorld deployment)
    private static string? _buildId;
    private static DateTime _buildIdFetchedAt = DateTime.MinValue;
    private static readonly TimeSpan BuildIdTtl = TimeSpan.FromHours(1);
    private static readonly SemaphoreSlim _buildIdLock = new(1, 1);

    public MakerWorldAdapter(HttpClient _, ILogger<MakerWorldAdapter> logger)
    {
        // We intentionally ignore the DI-supplied HttpClient as it gets blocked by Cloudflare.
        _logger = logger;
    }

    public ModelSource Source => ModelSource.MakerWorld;

    /// <summary>Create a standalone HttpClient that bypasses Cloudflare.</summary>
    private static HttpClient CreateBrowserClient()
    {
        var handler = new HttpClientHandler
        {
            AutomaticDecompression = DecompressionMethods.All,
            AllowAutoRedirect = true
        };
        var client = new HttpClient(handler) { Timeout = TimeSpan.FromSeconds(20) };
        client.DefaultRequestHeaders.Add("User-Agent",
            "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/120.0.0.0 Safari/537.36");
        client.DefaultRequestHeaders.Add("Accept",
            "text/html,application/xhtml+xml,application/xml;q=0.9,application/json,*/*;q=0.8");
        client.DefaultRequestHeaders.Add("Accept-Language", "en-US,en;q=0.9");
        return client;
    }

    public async Task<AdapterSearchResult> SearchAsync(string query, int page = 1, int pageSize = 20,
        string? sort = null, CancellationToken ct = default)
    {
        var result = new AdapterSearchResult { Source = "MakerWorld" };
        var offset = (page - 1) * pageSize;

        try
        {
            var buildId = await GetBuildIdAsync(ct);
            if (string.IsNullOrEmpty(buildId)) return result;

            var url = $"{BaseUrl}/_next/data/{buildId}/en/search/models.json?keyword={Uri.EscapeDataString(query)}&offset={offset}";
            
            var json = await FetchJsonAsync(url, ct);
            if (json == null) return result;

            if (!json.RootElement.TryGetProperty("pageProps", out var pageProps)) return result;

            result.TotalCount = pageProps.TryGetProperty("total", out var total) ? total.GetInt32() : 0;

            if (pageProps.TryGetProperty("designs", out var designs) && designs.ValueKind == JsonValueKind.Array)
            {
                foreach (var item in designs.EnumerateArray())
                {
                    result.Items.Add(MapToDto(item));
                }
            }

            _logger.LogInformation("MakerWorld: Found {Count} results (total: {Total}) for '{Query}'",
                result.Items.Count, result.TotalCount, query);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "MakerWorld search failed for '{Query}'", query);
        }

        return result;
    }

    public async Task<AdapterSearchResult> GetTrendingAsync(int page = 1, int pageSize = 20,
        CancellationToken ct = default)
    {
        var result = new AdapterSearchResult { Source = "MakerWorld" };
        var offset = (page - 1) * pageSize;

        try
        {
            var buildId = await GetBuildIdAsync(ct);
            if (string.IsNullOrEmpty(buildId)) return result;

            // Search without keyword returns trending/popular models
            var url = $"{BaseUrl}/_next/data/{buildId}/en/search/models.json?offset={offset}";

            var json = await FetchJsonAsync(url, ct);
            if (json == null) return result;

            if (!json.RootElement.TryGetProperty("pageProps", out var pageProps)) return result;

            result.TotalCount = pageProps.TryGetProperty("total", out var total) ? total.GetInt32() : 0;

            if (pageProps.TryGetProperty("designs", out var designs) && designs.ValueKind == JsonValueKind.Array)
            {
                foreach (var item in designs.EnumerateArray())
                {
                    result.Items.Add(MapToDto(item));
                }
            }

            _logger.LogInformation("MakerWorld: Found {Count} trending results", result.Items.Count);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "MakerWorld trending failed");
        }

        return result;
    }

    public async Task<ModelDto?> GetModelDetailsAsync(string externalId, CancellationToken ct = default)
    {
        try
        {
            var buildId = await GetBuildIdAsync(ct);
            if (string.IsNullOrEmpty(buildId)) return null;

            // externalId can be "40146" or "40146-benchy-bambu-pla-basic"
            var url = $"{BaseUrl}/_next/data/{buildId}/en/models/{externalId}.json";

            var json = await FetchJsonAsync(url, ct);
            if (json == null) return null;

            if (!json.RootElement.TryGetProperty("pageProps", out var pageProps)) return null;

            // Check for redirect (detail by ID only redirects to ID-slug form)
            if (pageProps.TryGetProperty("__N_REDIRECT", out var redirect))
            {
                var redirectPath = redirect.GetString();
                if (!string.IsNullOrEmpty(redirectPath))
                {
                    var redirectUrl = $"{BaseUrl}/_next/data/{buildId}{redirectPath}.json";
                    json = await FetchJsonAsync(redirectUrl, ct);
                    if (json == null) return null;
                    if (!json.RootElement.TryGetProperty("pageProps", out pageProps)) return null;
                }
            }

            if (!pageProps.TryGetProperty("design", out var design) || design.ValueKind != JsonValueKind.Object)
                return null;

            var dto = MapToDto(design);

            // Enrich with detail-specific fields
            if (design.TryGetProperty("description", out var desc) && desc.ValueKind == JsonValueKind.String)
            {
                dto.Description = desc.GetString() ?? "";
                dto.DescriptionHtml = dto.Description;
            }

            // Extract all images from designExtension.design_pictures
            if (design.TryGetProperty("designExtension", out var ext) && ext.ValueKind == JsonValueKind.Object
                && ext.TryGetProperty("design_pictures", out var pics) && pics.ValueKind == JsonValueKind.Array)
            {
                dto.ImageUrls = [];
                foreach (var pic in pics.EnumerateArray())
                {
                    if (pic.TryGetProperty("url", out var picUrl) && picUrl.ValueKind == JsonValueKind.String)
                    {
                        var imgUrl = picUrl.GetString();
                        if (!string.IsNullOrEmpty(imgUrl))
                            dto.ImageUrls.Add(imgUrl);
                    }
                }
                if (dto.ImageUrls.Count > 0)
                    dto.ThumbnailUrl = dto.ImageUrls[0];
            }

            return dto;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "MakerWorld details failed for {Id}", externalId);
            return null;
        }
    }

    #region Helpers

    /// <summary>Fetch and parse JSON from a URL using a standalone browser-like HttpClient.</summary>
    private async Task<JsonDocument?> FetchJsonAsync(string url, CancellationToken ct)
    {
        using var client = CreateBrowserClient();
        
        _logger.LogDebug("MakerWorld: Fetching {Url}", url);
        var response = await client.GetAsync(url, ct);

        // If 404, the buildId may have changed (new deployment)
        if (response.StatusCode == HttpStatusCode.NotFound)
        {
            _logger.LogWarning("MakerWorld: Got 404 for {Url}, refreshing buildId...", url);
            _buildId = null;
            return null;
        }

        if (response.StatusCode == HttpStatusCode.Forbidden)
        {
            _logger.LogWarning("MakerWorld: Got 403 (Cloudflare) for {Url}", url);
            return null;
        }

        response.EnsureSuccessStatusCode();
        return await JsonDocument.ParseAsync(await response.Content.ReadAsStreamAsync(ct), cancellationToken: ct);
    }

    /// <summary>Get or refresh the Next.js buildId from the MakerWorld homepage.</summary>
    private async Task<string?> GetBuildIdAsync(CancellationToken ct)
    {
        if (_buildId != null && DateTime.UtcNow - _buildIdFetchedAt < BuildIdTtl)
            return _buildId;

        await _buildIdLock.WaitAsync(ct);
        try
        {
            if (_buildId != null && DateTime.UtcNow - _buildIdFetchedAt < BuildIdTtl)
                return _buildId;

            using var client = CreateBrowserClient();
            
            _logger.LogInformation("MakerWorld: Fetching buildId from homepage...");
            var html = await client.GetStringAsync($"{BaseUrl}/en", ct);
            _logger.LogInformation("MakerWorld: Got homepage HTML ({Length} chars)", html.Length);
            
            var match = BuildIdRegex().Match(html);
            if (match.Success)
            {
                _buildId = match.Groups[1].Value;
                _buildIdFetchedAt = DateTime.UtcNow;
                _logger.LogInformation("MakerWorld: Fetched buildId = {BuildId}", _buildId);
                return _buildId;
            }

            _logger.LogError("MakerWorld: Could not extract buildId from HTML");
            return null;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "MakerWorld: Failed to fetch buildId");
            return null;
        }
        finally
        {
            _buildIdLock.Release();
        }
    }

    private static ModelDto MapToDto(JsonElement item)
    {
        var id = item.TryGetProperty("id", out var idProp)
            ? (idProp.ValueKind == JsonValueKind.Number ? idProp.GetInt64().ToString() : idProp.GetString() ?? "0")
            : "0";

        var slug = item.TryGetProperty("slug", out var s) ? s.GetString() ?? "" : "";
        var title = item.TryGetProperty("title", out var t) ? t.GetString() ?? "Untitled" : "Untitled";
        var cover = item.TryGetProperty("cover", out var c) ? c.GetString() ?? "" : "";

        // Creator
        var creatorName = "Unknown";
        var creatorHandle = "";
        if (item.TryGetProperty("designCreator", out var creator) && creator.ValueKind == JsonValueKind.Object)
        {
            creatorName = creator.TryGetProperty("name", out var cn) ? cn.GetString() ?? "Unknown" : "Unknown";
            creatorHandle = creator.TryGetProperty("handle", out var ch) ? ch.GetString() ?? "" : "";
        }

        var creatorProfileUrl = !string.IsNullOrEmpty(creatorHandle)
            ? $"https://makerworld.com/en/@{creatorHandle}"
            : "";

        // Tags
        var tags = new List<string>();
        if (item.TryGetProperty("tags", out var tagsEl) && tagsEl.ValueKind == JsonValueKind.Array)
        {
            foreach (var tag in tagsEl.EnumerateArray())
            {
                var tagStr = tag.ValueKind == JsonValueKind.String ? tag.GetString() : null;
                if (!string.IsNullOrEmpty(tagStr))
                    tags.Add(tagStr);
            }
        }

        // License
        var license = item.TryGetProperty("license", out var lic) ? lic.GetString() : null;

        // Source URL
        var sourceUrl = !string.IsNullOrEmpty(slug)
            ? $"{SiteBaseUrl}{id}-{slug}"
            : $"{SiteBaseUrl}{id}";

        // All MakerWorld models are free
        return new ModelDto
        {
            ExternalId = !string.IsNullOrEmpty(slug) ? $"{id}-{slug}" : id,
            Source = "MakerWorld",
            Title = title,
            SourceUrl = sourceUrl,
            ThumbnailUrl = cover,
            ImageUrls = string.IsNullOrEmpty(cover) ? [] : [cover],
            CreatorName = creatorName,
            CreatorProfileUrl = creatorProfileUrl,
            Price = 0,
            Currency = "USD",
            IsFree = true,
            LikeCount = item.TryGetProperty("likeCount", out var lc) ? lc.GetInt32() : 0,
            ViewCount = item.TryGetProperty("printCount", out var pc) ? pc.GetInt32() : 0,
            MakeCount = item.TryGetProperty("downloadCount", out var dc) ? dc.GetInt32() : 0,
            Tags = tags,
            License = license,
            CreatedAtSource = item.TryGetProperty("createTime", out var ct2) && ct2.ValueKind == JsonValueKind.String
                ? (DateTime.TryParse(ct2.GetString(), out var d) ? d : DateTime.UtcNow)
                : DateTime.UtcNow,
        };
    }

    #endregion

    [GeneratedRegex(@"""buildId""\s*:\s*""([^""]+)""")]
    private static partial Regex BuildIdRegex();
}
