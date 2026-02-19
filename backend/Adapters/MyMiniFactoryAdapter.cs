using System.Text.Json;
using ModelAggregator.Api.DTOs;
using ModelAggregator.Api.Models;

namespace ModelAggregator.Api.Adapters;

public class MyMiniFactoryAdapter : IModelSourceAdapter
{
    private readonly HttpClient _http;
    private readonly string _apiKey;
    private readonly ILogger<MyMiniFactoryAdapter> _logger;

    public MyMiniFactoryAdapter(HttpClient http, IConfiguration config, ILogger<MyMiniFactoryAdapter> logger)
    {
        _http = http;
        _apiKey = config["MyMiniFactory:ApiKey"] ?? "";
        _logger = logger;
        _http.BaseAddress = new Uri("https://www.myminifactory.com/api/v2/");
        _http.DefaultRequestHeaders.Add("User-Agent", "3DSearchAggregator/1.0");
    }

    public ModelSource Source => ModelSource.MyMiniFactory;

    public async Task<AdapterSearchResult> SearchAsync(string query, int page = 1, int pageSize = 20, string? sort = null, CancellationToken ct = default)
    {
        var result = new AdapterSearchResult { Source = "MyMiniFactory" };
        try
        {
            var apiSort = sort switch
            {
                "newest" => "date",
                "popular" => "popularity",
                "likes" => "popularity",
                _ => ""
            };

            var sortParam = string.IsNullOrEmpty(apiSort) ? "" : $"&sort={apiSort}";
            var url = $"search?q={Uri.EscapeDataString(query)}&key={_apiKey}&page={page}&per_page={pageSize}{sortParam}";
            var response = await _http.GetAsync(url, ct);
            response.EnsureSuccessStatusCode();

            using var doc = await JsonDocument.ParseAsync(await response.Content.ReadAsStreamAsync(ct), cancellationToken: ct);
            var root = doc.RootElement;

            if (root.TryGetProperty("total_count", out var totalProp))
                result.TotalCount = totalProp.GetInt32();
            else if (root.TryGetProperty("Count", out var countProp))
                result.TotalCount = countProp.GetInt32();

            if (root.TryGetProperty("items", out var items) && items.ValueKind == JsonValueKind.Array)
            {
                foreach (var item in items.EnumerateArray())
                {
                    result.Items.Add(MapToDto(item));
                }
            }

            _logger.LogInformation("MyMiniFactory: Found {Count} results (total: {Total})", result.Items.Count, result.TotalCount);
            return result;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "MyMiniFactory search failed for '{Query}'", query);
            return result;
        }
    }

    public async Task<AdapterSearchResult> GetTrendingAsync(int page = 1, int pageSize = 20, CancellationToken ct = default)
    {
        var result = new AdapterSearchResult { Source = "MyMiniFactory" };
        try
        {
            // MMF trending is best approximated by 'popular' sort on all objects
            // The 'objects' endpoint with sort=popular returns 0 likes, so we use the search endpoint instead.
            var url = $"search?q=&key={_apiKey}&page={page}&per_page={pageSize}&sort=popularity";
            var response = await _http.GetAsync(url, ct);
            
            response.EnsureSuccessStatusCode();

            using var doc = await JsonDocument.ParseAsync(await response.Content.ReadAsStreamAsync(ct), cancellationToken: ct);
            var root = doc.RootElement;

            // Handle both array root and { items: [] } object root
            var items = root.ValueKind == JsonValueKind.Array ? root : (root.TryGetProperty("items", out var it) ? it : root);

            if (items.ValueKind == JsonValueKind.Array)
            {
                foreach (var item in items.EnumerateArray())
                {
                    result.Items.Add(MapToDto(item));
                }
            }

            result.TotalCount = root.TryGetProperty("total_count", out var t) ? t.GetInt32() :
                                root.TryGetProperty("total", out var tot) ? tot.GetInt32() : result.Items.Count;

            _logger.LogInformation("MyMiniFactory: Found {Count} popular results", result.Items.Count);
            return result;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "MyMiniFactory trending failed");
            return result;
        }
    }

    private ModelDto MapToDto(JsonElement item)
    {
        var url = GetUrl(item, "url") ?? "";
        // ID correction: MMF search API returns an 'id' that might not be the object ID.
        // We try to extract the real ID from the URL (e.g. .../object/name-12345)
        var id = item.TryGetProperty("id", out var idProp) ? idProp.ToString() : "0";
        if (!string.IsNullOrEmpty(url))
        {
            var parts = url.Split('-');
            if (parts.Length > 0 && long.TryParse(parts.Last(), out var urlId))
            {
                id = urlId.ToString();
            }
        }
        var name = GetUrl(item, "name") ?? "Untitled";

        // Try to get a high-res image via heuristic (MMF uses "insecure/rt:fill-down/w:200/h:200/...")
        string thumbUrl = "";
        if (item.TryGetProperty("images", out var imgs) && imgs.ValueKind == JsonValueKind.Array && imgs.GetArrayLength() > 0)
        {
            var firstImg = imgs[0];
            thumbUrl = GetUrl(firstImg, "standard") ?? GetUrl(firstImg, "large") ?? GetUrl(firstImg, "url") ?? "";
        }
        
        if (string.IsNullOrEmpty(thumbUrl))
        {
            thumbUrl = GetUrl(item, "thumbnail") ?? "";
        }

        // Apply high-res boost
        if (thumbUrl.Contains("/w:200/h:200/")) thumbUrl = thumbUrl.Replace("/w:200/h:200/", "/w:600/h:600/");
        else if (thumbUrl.Contains("/w:400/h:400/")) thumbUrl = thumbUrl.Replace("/w:400/h:400/", "/w:600/h:600/");
        
        // Final fallback: use designer avatar
        if (string.IsNullOrEmpty(thumbUrl) && item.TryGetProperty("designer", out var dsgn))
        {
            thumbUrl = GetUrl(dsgn, "avatar_url") ?? "";
        }

        decimal price = 0;
        if (item.TryGetProperty("price", out var p))
        {
            if (p.ValueKind == JsonValueKind.Object && p.TryGetProperty("value", out var v) && v.ValueKind == JsonValueKind.Number)
                price = v.GetDecimal();
            else if (p.ValueKind == JsonValueKind.Number)
                price = p.GetDecimal();
        }

        return new ModelDto
        {
            ExternalId = id,
            Source = "MyMiniFactory",
            Title = name,
            Description = GetUrl(item, "description") ?? "",
            SourceUrl = url,
            ThumbnailUrl = thumbUrl,
            CreatorName = item.TryGetProperty("designer", out var d) ? GetUrl(d, "username") ?? "Unknown" : "Unknown",
            CreatorProfileUrl = item.TryGetProperty("designer", out var d2) ? GetUrl(d2, "profile_url") ?? "" : "",
            Price = price,
            IsFree = price == 0,
            // DownloadCount removed
            LikeCount = item.TryGetProperty("likes", out var l) && l.ValueKind == JsonValueKind.Number ? l.GetInt32() : 0,
            CreatedAtSource = item.TryGetProperty("published_at", out var pa) && pa.ValueKind == JsonValueKind.String && pa.TryGetDateTime(out var dt) ? dt : DateTime.MinValue,
            Tags = (item.TryGetProperty("tags", out var tg) && tg.ValueKind == JsonValueKind.Array)
                   ? tg.EnumerateArray().Select(x => x.ValueKind == JsonValueKind.String ? x.GetString() ?? "" : "").Where(s => !string.IsNullOrEmpty(s)).ToList() : [],
            ImageUrls = [thumbUrl]
        };
    }

    public async Task<ModelDto?> GetModelDetailsAsync(string externalId, CancellationToken ct = default)
    {
        try
        {
            // MMF detail endpoint: /objects/{id}
            var url = $"objects/{externalId}?key={_apiKey}";
            var response = await _http.GetAsync(url, ct);
            if (!response.IsSuccessStatusCode) return null;

            using var doc = await JsonDocument.ParseAsync(await response.Content.ReadAsStreamAsync(ct), cancellationToken: ct);
            var root = doc.RootElement;
            
            var dto = MapToDto(root);
            
            dto.ViewCount = root.TryGetProperty("views", out var vc) ? vc.GetInt32() : 0;
            dto.DescriptionHtml = root.TryGetProperty("description_html", out var dh) ? dh.GetString() : dto.Description;

            // Images
            if (root.TryGetProperty("images", out var imgs) && imgs.ValueKind == JsonValueKind.Array)
            {
                var list = new List<string>();
                foreach(var img in imgs.EnumerateArray())
                {
                    // Heuristic for fetching good definition images
                    var u = GetUrl(img, "standard") ?? GetUrl(img, "large") ?? GetUrl(img, "url");
                    if (!string.IsNullOrEmpty(u)) list.Add(u);
                }
                if (list.Count > 0) dto.ImageUrls = list;
            }

            return dto;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "MyMiniFactory details fetch failed for {Id}", externalId);
            return null;
        }
    }

    private static string? GetUrl(JsonElement element, string prop)
    {
        if (element.TryGetProperty(prop, out var val))
        {
            if (val.ValueKind == JsonValueKind.String) return val.GetString();
            if (val.ValueKind == JsonValueKind.Object && val.TryGetProperty("url", out var u)) return u.GetString();
        }
        return null;
    }
}
