using System.Net;
using System.Net.Http;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using ModelAggregator.Api.DTOs;
using ModelAggregator.Api.Models;

namespace ModelAggregator.Api.Adapters;

/// <summary>
/// Adapter for MakerWorld (Bambu Lab) using the direct REST API.
/// Uses /api/v1/search-service/select/design2 for search/trending
/// and /api/v1/design/{id} for model details.
/// </summary>
public class MakerWorldAdapter : IModelSourceAdapter
{
    private readonly ILogger<MakerWorldAdapter> _logger;
    private readonly HttpClient _http;
    private const string BaseUrl = "https://makerworld.com";
    private const string SearchApiUrl = $"{BaseUrl}/api/v1/search-service/select/design2";
    private const string SiteBaseUrl = "https://makerworld.com/en/models/";

    public MakerWorldAdapter(HttpClient http, ILogger<MakerWorldAdapter> logger)
    {
        _http = http;
        _logger = logger;
    }

    public ModelSource Source => ModelSource.MakerWorld;

    /// <summary>Ensures the standard browser headers are present.</summary>
    private void EnsureHeaders()
    {
        _http.DefaultRequestHeaders.Clear();

        // Use a mobile User-Agent (often less strictly challenged)
        _http.DefaultRequestHeaders.Add("User-Agent",
            "Mozilla/5.0 (Linux; Android 14; Pixel 8) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/133.0.0.0 Mobile Safari/537.36");

        _http.DefaultRequestHeaders.Add("Accept", "application/json, text/plain, */*");
        _http.DefaultRequestHeaders.Add("Accept-Language", "en-US,en;q=0.9");
        
        _http.DefaultRequestHeaders.Referrer = new Uri(BaseUrl);

        _http.DefaultRequestHeaders.Add("Origin", BaseUrl);

        _http.DefaultRequestHeaders.Add("Sec-Ch-Ua",
            "\"Not(A:Brand\";v=\"99\", \"Google Chrome\";v=\"133\", \"Chromium\";v=\"133\"");
        _http.DefaultRequestHeaders.Add("Sec-Ch-Ua-Mobile", "?1");
        _http.DefaultRequestHeaders.Add("Sec-Ch-Ua-Platform", "\"Android\"");
        
        _http.DefaultRequestHeaders.Add("Sec-Fetch-Site", "same-origin");
        _http.DefaultRequestHeaders.Add("Sec-Fetch-Mode", "cors");
        _http.DefaultRequestHeaders.Add("Sec-Fetch-Dest", "empty");
        
        // Add additional browser headers
        _http.DefaultRequestHeaders.Add("Priority", "u=1, i");
        _http.DefaultRequestHeaders.Add("DNT", "1");
    }

    public async Task<AdapterSearchResult> SearchAsync(string query, int page = 1, int pageSize = 20,
        string? sort = null, CancellationToken ct = default)
    {
        var result = new AdapterSearchResult { Source = "MakerWorld" };
        var offset = (page - 1) * pageSize;

        try
        {
            // Map sort parameter to MakerWorld's API sort field
            var apiSort = sort switch
            {
                "likes" => "&sortField=likesCount&sortOrder=desc",
                "popular" => "&sortField=downloadCount&sortOrder=desc",
                "newest" => "&sortField=createTime&sortOrder=desc",
                _ => ""  // default relevance
            };

            var url = $"{SearchApiUrl}?keyword={Uri.EscapeDataString(query)}&limit={pageSize}&offset={offset}{apiSort}";

            var json = await FetchJsonAsync(url, ct);
            if (json == null) return result;

            var root = json.RootElement;
            result.TotalCount = root.TryGetProperty("total", out var total) ? total.GetInt32() : 0;

            if (root.TryGetProperty("hits", out var hits) && hits.ValueKind == JsonValueKind.Array)
            {
                foreach (var item in hits.EnumerateArray())
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
            // Empty keyword with sort=popular returns trending models
            var url = $"{SearchApiUrl}?keyword=&limit={pageSize}&offset={offset}";

            var json = await FetchJsonAsync(url, ct);
            if (json == null) return result;

            var root = json.RootElement;
            result.TotalCount = root.TryGetProperty("total", out var total) ? total.GetInt32() : 0;

            if (root.TryGetProperty("hits", out var hits) && hits.ValueKind == JsonValueKind.Array)
            {
                foreach (var item in hits.EnumerateArray())
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
            // externalId can be "686760-cat" or "686760"
            // Extract the slug part (after first dash) to use as search keyword
            var parts = externalId.Split('-', 2);
            var slug = parts.Length > 1 ? parts[1].Replace("-", " ") : parts[0];

            // Use the search API to find the model (the search response includes full image data)
            var url = $"{SearchApiUrl}?keyword={Uri.EscapeDataString(slug)}&limit=5&offset=0";
            var json = await FetchJsonAsync(url, ct);
            if (json == null) return null;

            var root = json.RootElement;
            if (!root.TryGetProperty("hits", out var hits) || hits.ValueKind != JsonValueKind.Array)
                return null;

            // Find the matching model by ID
            var numericId = parts[0];
            JsonElement? matchingDesign = null;
            foreach (var item in hits.EnumerateArray())
            {
                if (item.TryGetProperty("id", out var idProp))
                {
                    var itemId = idProp.ValueKind == JsonValueKind.Number
                        ? idProp.GetInt64().ToString()
                        : idProp.GetString() ?? "";
                    if (itemId == numericId)
                    {
                        matchingDesign = item;
                        break;
                    }
                }
            }

            // If exact match not found, use the first result as a fallback
            if (matchingDesign == null)
            {
                var enumerator = hits.EnumerateArray().GetEnumerator();
                if (enumerator.MoveNext())
                    matchingDesign = enumerator.Current;
                else
                    return null;
            }

            var design = matchingDesign.Value;
            var dto = MapToDto(design);

            // Enrich with all images from designExtension.design_pictures
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

    /// <summary>Fetch and parse JSON from a URL.</summary>
    private async Task<JsonDocument?> FetchJsonAsync(string url, CancellationToken ct)
    {
        EnsureHeaders();

        _logger.LogDebug("MakerWorld: Fetching {Url}", url);
        
        var request = new HttpRequestMessage(HttpMethod.Get, url)
        {
            Version = new Version(2, 0),
            VersionPolicy = HttpVersionPolicy.RequestVersionOrHigher
        };

        var response = await _http.SendAsync(request, ct);

        if (response.StatusCode == HttpStatusCode.Forbidden)
        {
            _logger.LogError("MakerWorld: Got 403 (Cloudflare) for {Url}. Blocked.", url);
            return null;
        }

        if (response.StatusCode == HttpStatusCode.NotFound)
        {
            _logger.LogWarning("MakerWorld: Got 404 for {Url}.", url);
            return null;
        }

        response.EnsureSuccessStatusCode();
        return await JsonDocument.ParseAsync(await response.Content.ReadAsStreamAsync(ct), cancellationToken: ct);
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
}
