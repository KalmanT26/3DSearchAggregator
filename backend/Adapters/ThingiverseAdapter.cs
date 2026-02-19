using System.Net.Http.Headers;
using System.Text.Json;
using ModelAggregator.Api.DTOs;
using ModelAggregator.Api.Models;

namespace ModelAggregator.Api.Adapters;

public class ThingiverseAdapter : IModelSourceAdapter
{
    private readonly HttpClient _http;
    private readonly string _token;
    private readonly ILogger<ThingiverseAdapter> _logger;

    public ThingiverseAdapter(HttpClient http, IConfiguration config, ILogger<ThingiverseAdapter> logger)
    {
        _http = http;
        _token = config["Thingiverse:Token"] ?? "";
        _logger = logger;

        _http.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", _token);
        _http.BaseAddress = new Uri("https://api.thingiverse.com/");
        _http.DefaultRequestHeaders.Add("User-Agent", "3DSearchAggregator/1.0");
    }

    public ModelSource Source => ModelSource.Thingiverse;

    public async Task<AdapterSearchResult> SearchAsync(string query, int page = 1, int pageSize = 20, string? sort = null, CancellationToken ct = default)
    {
        var result = new AdapterSearchResult { Source = "Thingiverse" };
        var apiSort = sort switch
        {
            "newest" => "newest",
            "popular" => "popular",
            "likes" => "popular", // Closest proxy for likes
            _ => "relevant"
        };

        try
        {
            // Thingiverse search endpoint often uses search/{term}
            var url = $"search/{Uri.EscapeDataString(query)}?types=things&page={page}&per_page={pageSize}&sort={apiSort}";
            var response = await _http.GetAsync(url, ct);
            response.EnsureSuccessStatusCode();

            using var doc = await JsonDocument.ParseAsync(await response.Content.ReadAsStreamAsync(ct), cancellationToken: ct);
            var root = doc.RootElement;

            // Search results might be in a wrapper or direct array
            var items = root.ValueKind == JsonValueKind.Array ? root :
                        root.TryGetProperty("hits", out var hits) ? hits : root;

            if (root.TryGetProperty("total", out var total))
                result.TotalCount = total.GetInt32();
            else if (items.ValueKind == JsonValueKind.Array)
                result.TotalCount = items.GetArrayLength(); // Fallback

                        if (items.ValueKind == JsonValueKind.Array)
            {
                foreach (var item in items.EnumerateArray())
                {
                    result.Items.Add(MapToDto(item));
                }
            }

            _logger.LogInformation("Thingiverse: Found {Count} results (total: {Total})", result.Items.Count, result.TotalCount);
            return result;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Thingiverse search failed for '{Query}'", query);
            return result;
        }
    }



    public async Task<AdapterSearchResult> GetTrendingAsync(int page = 1, int pageSize = 20, CancellationToken ct = default)
    {
        var result = new AdapterSearchResult { Source = "Thingiverse" };
        try
        {
            // /popular returns a list of things directly
            var url = $"featured?page={page}&per_page={pageSize}";
            var response = await _http.GetAsync(url, ct);
            response.EnsureSuccessStatusCode();

            using var doc = await JsonDocument.ParseAsync(await response.Content.ReadAsStreamAsync(ct), cancellationToken: ct);
            var items = doc.RootElement;

                        if (items.ValueKind == JsonValueKind.Array)
            {
                result.TotalCount = 10000; // API pagination cap or estimate
                foreach (var item in items.EnumerateArray())
                {
                    result.Items.Add(MapToDto(item));
                }
            }

            _logger.LogInformation("Thingiverse: Found {Count} popular results", result.Items.Count);
            return result;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Thingiverse trending failed");
            return result;
        }
    }

    public async Task<ModelDto?> GetModelDetailsAsync(string externalId, CancellationToken ct = default)
    {
        try
        {
            var url = $"things/{externalId}";
            var response = await _http.GetAsync(url, ct);
            if (!response.IsSuccessStatusCode) return null;

            using var doc = await JsonDocument.ParseAsync(await response.Content.ReadAsStreamAsync(ct), cancellationToken: ct);
            var root = doc.RootElement;
            
            // Re-use MapToDto but enrich with detail-specific fields
            var dto = MapToDto(root);

            // Enrich with details
            dto.DescriptionHtml = root.TryGetProperty("description_html", out var dh) ? dh.GetString() : 
                                  root.TryGetProperty("description", out var d) ? d.GetString() : "";
            
            dto.ViewCount = root.TryGetProperty("view_count", out var vc) ? vc.GetInt32() : 0;
            dto.MakeCount = root.TryGetProperty("make_count", out var mc) ? mc.GetInt32() : 0;
            dto.FileCount = root.TryGetProperty("file_count", out var fc) ? fc.GetInt32() : 0;

            // Sometimes details has better images array
            if (root.TryGetProperty("images_url", out var imgUrlProp))
            {
                 // We could fetch images_url here if we wanted to be super thorough, 
                 // but for now let's stick to what's in the main object or just the thumbnail/preview
                 // Thingiverse details usually have 'default_image' which is good.
            }

            return dto;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Thingiverse details fetch failed for {Id}", externalId);
            return null;
        }
    }

    private static ModelDto MapToDto(JsonElement thing)
    {
        var id = thing.GetProperty("id").GetInt32().ToString();
        var isFree = !thing.TryGetProperty("is_free", out var free) || free.GetBoolean();
        
        // Improve image quality: Thingiverse thumbnails are often 150-300px.
        // Prefer preview_image if available (usually card_preview_ or similar), then fallback to thumbnail.
        string thumbUrl = thing.TryGetProperty("preview_image", out var preview) ? preview.GetString() ?? "" :
                          thing.TryGetProperty("thumbnail", out var thumb) ? thumb.GetString() ?? "" : "";

        if (!string.IsNullOrEmpty(thumbUrl))
        {
            var ext = Path.GetExtension(thumbUrl);
            // Suffix pattern (renders)
            thumbUrl = thumbUrl.Replace("_thumb_medium" + ext, "_display_large" + ext, StringComparison.OrdinalIgnoreCase)
                               .Replace("_thumb_small" + ext, "_display_large" + ext, StringComparison.OrdinalIgnoreCase)
                               .Replace("_thumb_tiny" + ext, "_display_large" + ext, StringComparison.OrdinalIgnoreCase);

            // Prefix pattern (assets)
            thumbUrl = thumbUrl.Replace("medium_thumb_", "card_preview_", StringComparison.OrdinalIgnoreCase)
                               .Replace("small_thumb_", "card_preview_", StringComparison.OrdinalIgnoreCase)
                               .Replace("tiny_thumb_", "card_preview_", StringComparison.OrdinalIgnoreCase);
        }

        // Thingiverse search hits don't always have download_count, but often have collect_count
        var likes = thing.TryGetProperty("like_count", out var lc) ? lc.GetInt32() : 
                    thing.TryGetProperty("collect_count", out var cc) ? cc.GetInt32() : 0;

        return new ModelDto
        {
            ExternalId = id,
            Source = "Thingiverse",
            Title = thing.GetProperty("name").GetString() ?? "Untitled",
            Description = thing.TryGetProperty("description", out var desc) ? desc.GetString() ?? "" : "",
            ThumbnailUrl = thumbUrl,
            SourceUrl = thing.TryGetProperty("public_url", out var pUrl) ? pUrl.GetString() ?? "" : $"https://www.thingiverse.com/thing:{id}",
            CreatorName = thing.TryGetProperty("creator", out var c) && c.TryGetProperty("name", out var n) ? n.GetString() ?? "Unknown" : "Unknown",
            CreatorProfileUrl = thing.TryGetProperty("creator", out var cr) && cr.TryGetProperty("public_url", out var cu) ? cu.GetString() ?? "" : "",
            Price = 0,
            IsFree = isFree,
            // DownloadCount removed
            LikeCount = likes,
            CreatedAtSource = thing.TryGetProperty("created_at", out var created) ? created.GetDateTime() : DateTime.MinValue,
            Tags = thing.TryGetProperty("tags", out var t) && t.ValueKind == JsonValueKind.Array
                   ? t.EnumerateArray().Select(tag => tag.TryGetProperty("name", out var tn) ? tn.GetString() ?? "" : "").Where(x => !string.IsNullOrEmpty(x)).ToList()
                   : [],
            ImageUrls = [thumbUrl]
        };
    }
}

