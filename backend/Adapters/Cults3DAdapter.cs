using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using ModelAggregator.Api.DTOs;
using ModelAggregator.Api.Models;

namespace ModelAggregator.Api.Adapters;

public partial class Cults3DAdapter : IModelSourceAdapter
{
    private readonly HttpClient _http;
    private readonly ILogger<Cults3DAdapter> _logger;

    public Cults3DAdapter(HttpClient http, IConfiguration config, ILogger<Cults3DAdapter> logger)
    {
        _http = http;
        var user = config["Cults3D:Username"] ?? "";
        // Remove @ if present for auth
        if (user.StartsWith('@')) user = user[1..];

        var key = config["Cults3D:ApiKey"] ?? "";
        _logger = logger;

        var basic = Convert.ToBase64String(Encoding.UTF8.GetBytes($"{user}:{key}"));
        _http.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Basic", basic);
        _http.BaseAddress = new Uri("https://cults3d.com/");
        _http.DefaultRequestHeaders.Add("User-Agent", "3DSearchAggregator/1.0");
    }

    public ModelSource Source => ModelSource.Cults3D;

    public async Task<AdapterSearchResult> SearchAsync(string query, int page = 1, int pageSize = 20, string? sort = null, CancellationToken ct = default)
    {
        var result = new AdapterSearchResult { Source = "Cults3D" };
        var limit = pageSize;
        var offset = (page - 1) * pageSize;

        // Map sort to Cults3D GraphQL Enums
        var (sortEnum, sortDir) = sort switch
        {
            "newest" => ("BY_PUBLICATION", "DESC"),
            "likes" => ("BY_LIKES", "DESC"), 
            "popular" => ("BY_DOWNLOADS", "DESC"),
            "price_asc" => ("BY_PRICE", "ASC"),
            "price_desc" => ("BY_PRICE", "DESC"),
            _ => ("", "") // Relevance
        };

        var sortArg = !string.IsNullOrEmpty(sortEnum) ? $", sort: {sortEnum}, direction: {sortDir}" : "";

        var gql = new
        {
            query = $@"query Search($q: String!, $limit: Int!, $offset: Int!) {{ 
                creationsSearchBatch(query: $q, limit: $limit, offset: $offset{sortArg}) {{ 
                    total
                    results {{ 
                        id slug name description 
                        price(currency: EUR) {{ value }}
                        creator {{ nick shortUrl }} 
                        illustrationImageUrl(version: DEFAULT)
                        publishedAt downloadsCount likesCount
                    }} 
                }} 
            }}",
            variables = new { q = query, limit, offset }
        };

        try
        {
            var content = new StringContent(JsonSerializer.Serialize(gql), Encoding.UTF8, "application/json");
            var response = await _http.PostAsync("graphql", content, ct);
            response.EnsureSuccessStatusCode();

            using var doc = await JsonDocument.ParseAsync(await response.Content.ReadAsStreamAsync(ct), cancellationToken: ct);
            var root = doc.RootElement;
            
            if (root.TryGetProperty("errors", out var errors))
            {
                _logger.LogError("Cults3D GraphQL errors: {Errors}", errors.ToString());
                return result;
            }

            if (!root.TryGetProperty("data", out var data) || !data.TryGetProperty("creationsSearchBatch", out var searchBatch))
            {
                _logger.LogWarning("Cults3D search returned no 'creationsSearchBatch' block.");
                return result;
            }

            result.TotalCount = searchBatch.TryGetProperty("total", out var t) ? t.GetInt32() : 0;
            
            if (searchBatch.TryGetProperty("results", out var creations) && creations.ValueKind == JsonValueKind.Array)
            {
                foreach (var item in creations.EnumerateArray())
                {
                    result.Items.Add(MapToDto(item));
                }
            }

            _logger.LogInformation("Cults3D: Found {Count} results (total: {Total})", result.Items.Count, result.TotalCount);
            return result;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Cults3D search failed for '{Query}'", query);
            return result;
        }
    }

    public async Task<AdapterSearchResult> GetTrendingAsync(int page = 1, int pageSize = 20, CancellationToken ct = default)
    {
        var result = new AdapterSearchResult { Source = "Cults3D" };
        var limit = pageSize;
        var offset = (page - 1) * pageSize;

        var gql = new
        {
            query = @"query Trending($limit: Int!, $offset: Int!) { 
                creationsSearchBatch(query: """", limit: $limit, offset: $offset) { 
                    total
                    results { 
                        id slug name description
                        price(currency: EUR) { value }
                        creator { nick shortUrl } 
                        illustrationImageUrl(version: DEFAULT)
                        publishedAt downloadsCount likesCount
                    } 
                } 
            }",
            variables = new { limit, offset }
        };

        try
        {
            var content = new StringContent(JsonSerializer.Serialize(gql), Encoding.UTF8, "application/json");
            var response = await _http.PostAsync("graphql", content, ct);
            response.EnsureSuccessStatusCode();

            using var doc = await JsonDocument.ParseAsync(await response.Content.ReadAsStreamAsync(ct), cancellationToken: ct);
            var root = doc.RootElement;
            
            if (root.TryGetProperty("errors", out var errors))
            {
                _logger.LogError("Cults3D GraphQL trending errors: {Errors}", errors.ToString());
                return result;
            }

            if (!root.TryGetProperty("data", out var data) || !data.TryGetProperty("creationsSearchBatch", out var batch))
            {
                _logger.LogWarning("Cults3D trending returned no 'creationsSearchBatch' block.");
                return result;
            }

            result.TotalCount = batch.TryGetProperty("total", out var t) ? t.GetInt32() : 0;

            if (batch.TryGetProperty("results", out var creations) && creations.ValueKind == JsonValueKind.Array)
            {
                foreach (var item in creations.EnumerateArray())
                {
                    result.Items.Add(MapToDto(item));
                }
            }
            
            _logger.LogInformation("Cults3D: Found {Count} trending results (total: {Total})", result.Items.Count, result.TotalCount);
            return result;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Cults3D trending failed");
            return result;
        }
    }

    public async Task<ModelDto?> GetModelDetailsAsync(string externalId, CancellationToken ct = default)
    {
        ModelDto? dto = null;

        // 1. Try GraphQL first
        try
        {
            var gql = new
            {
                query = @"query GetCreation($slug: String!) { 
                    creation(slug: $slug) { 
                        id slug name description 
                        price(currency: EUR) { value }
                        creator { nick shortUrl } 
                        illustrationImageUrl(version: DEFAULT)
                        publishedAt downloadsCount likesCount
                    } 
                }",
                variables = new { slug = externalId }
            };

            var content = new StringContent(JsonSerializer.Serialize(gql), Encoding.UTF8, "application/json");
            var response = await _http.PostAsync("graphql", content, ct);
            
            if (response.IsSuccessStatusCode)
            {
                using var doc = await JsonDocument.ParseAsync(await response.Content.ReadAsStreamAsync(ct), cancellationToken: ct);
                var root = doc.RootElement;
                if (root.TryGetProperty("data", out var data) && data.TryGetProperty("creation", out var creation) && creation.ValueKind != JsonValueKind.Null)
                {
                    dto = MapToDto(creation);
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Cults3D GraphQL details fetch failed for {Id}", externalId);
        }

        // 2. If GraphQL failed or returned NO description, fall back to scraping
        if (dto == null || string.IsNullOrWhiteSpace(dto.Description))
        {
            var scraped = await ScrapeDetailsAsync(externalId, ct);
            if (scraped != null)
            {
                if (dto == null)
                {
                    dto = scraped;
                }
                else
                {
                    // Merge: prefer scraped description if it's longer/exists
                    if (!string.IsNullOrWhiteSpace(scraped.Description))
                    {
                        dto.Description = scraped.Description;
                        dto.DescriptionHtml = scraped.DescriptionHtml;
                    }
                    if (scraped.ImageUrls.Count > dto.ImageUrls.Count)
                    {
                        dto.ImageUrls = scraped.ImageUrls;
                        dto.ThumbnailUrl = scraped.ThumbnailUrl;
                    }
                    if (dto.CreatorName == "Unknown" && scraped.CreatorName != "Unknown")
                    {
                        dto.CreatorName = scraped.CreatorName;
                        dto.CreatorProfileUrl = scraped.CreatorProfileUrl;
                    }
                }
            }
        }

        return dto;
    }

    private async Task<ModelDto?> ScrapeDetailsAsync(string slug, CancellationToken ct)
    {
        try
        {
            // Construct URL - 'various' is catch-all, server redirects if category differs
            var url = $"en/3d-model/various/{slug}"; // BaseAddress is set to https://cults3d.com/
            var response = await _http.GetAsync(url, ct);
            if (!response.IsSuccessStatusCode) return null;

            var html = await response.Content.ReadAsStringAsync(ct);
            var dto = new ModelDto 
            { 
                ExternalId = slug, 
                Source = "Cults3D",
                SourceUrl = $"https://cults3d.com/{url}"
            };

            // Stage A: JSON-LD Extraction (Most reliable for metadata)
            var jsonLdMatch = JsonLdRegex().Match(html);
            if (jsonLdMatch.Success)
            {
                try
                {
                    using var jdoc = JsonDocument.Parse(jsonLdMatch.Groups[1].Value);
                    var root = jdoc.RootElement;
                    if (root.TryGetProperty("name", out var name)) dto.Title = name.GetString() ?? slug;
                    if (root.TryGetProperty("description", out var desc)) dto.Description = desc.GetString() ?? "";
                    if (root.TryGetProperty("image", out var img))
                    {
                        if (img.ValueKind == JsonValueKind.Array && img.GetArrayLength() > 0)
                            dto.ThumbnailUrl = img[0].GetString() ?? "";
                        else if (img.ValueKind == JsonValueKind.String)
                            dto.ThumbnailUrl = img.GetString() ?? "";
                    }
                }
                catch { /* fallback to other methods */ }
            }

            // Stage B: Enhanced Regex Extraction
            if (string.IsNullOrEmpty(dto.Title))
            {
                var titleMatch = TitleRegex().Match(html);
                dto.Title = titleMatch.Success ? System.Net.WebUtility.HtmlDecode(titleMatch.Groups[1].Value.Trim()) : slug;
            }

            if (string.IsNullOrWhiteSpace(dto.Description))
            {
                // Try multiple patterns for description
                // 1. Markdown-style header in rendered HTML
                var descHeaderMatch = DescriptionHeaderRegex().Match(html);
                if (descHeaderMatch.Success)
                {
                    dto.Description = descHeaderMatch.Groups[1].Value.Trim();
                }
                else
                {
                    // 2. The 'read-more-text' box
                    var descBoxMatch = DescriptionBoxRegex().Match(html);
                    if (descBoxMatch.Success)
                    {
                        dto.DescriptionHtml = descBoxMatch.Groups[1].Value.Trim();
                        dto.Description = HtmlTagRegex().Replace(dto.DescriptionHtml, "").Trim();
                    }
                }
            }

            // Image fallback via og:image
            if (string.IsNullOrEmpty(dto.ThumbnailUrl))
            {
                var imgMatch = OgImageRegex().Match(html);
                if (imgMatch.Success) dto.ThumbnailUrl = imgMatch.Groups[1].Value;
            }
            if (!string.IsNullOrEmpty(dto.ThumbnailUrl)) dto.ImageUrls = [dto.ThumbnailUrl];

            // Price/Free
            if (html.Contains("data-price=\"0\"") || html.Contains(">Free<") || html.Contains(">Gratuit<"))
            {
                dto.IsFree = true;
                dto.Price = 0;
            }

            // Creator
            var creatorMatch = CreatorRegex().Match(html);
            if (creatorMatch.Success)
            {
                 dto.CreatorName = creatorMatch.Groups[1].Value;
                 dto.CreatorProfileUrl = $"https://cults3d.com/en/users/{dto.CreatorName}";
            }

            return dto;
        }
        catch (Exception ex)
        {
             _logger.LogError(ex, "Cults3D scraping failed for {Slug}", slug);
             return null;
        }
    }

    private static ModelDto MapToDto(JsonElement item)
    {
        // Safely extract ID (handle string or number)
        string id = "0";
        if (item.TryGetProperty("id", out var idProp))
        {
            id = idProp.ValueKind == JsonValueKind.Number ? idProp.GetInt64().ToString() : (idProp.GetString() ?? "0");
        }
        
        // Extract slug if available - fallback to ID
        string slug = item.TryGetProperty("slug", out var s) ? (s.GetString() ?? id) : id;
        
        // ExternalId: Use slug if valid, otherwise ID
        var externalId = !string.IsNullOrEmpty(slug) && slug != "0" ? slug : id;

        var name = item.TryGetProperty("name", out var n) ? n.GetString() : "Untitled";
        var description = item.TryGetProperty("description", out var ds) ? ds.GetString() : "";
        
        decimal price = 0;
        if (item.TryGetProperty("price", out var p) && p.TryGetProperty("value", out var v))
        {
            price = v.GetDecimal();
        }

        var thumbUrl = item.TryGetProperty("illustrationImageUrl", out var img) ? img.GetString() ?? "" : "";

        var creator = item.TryGetProperty("creator", out var c) ? c : default;
        var creatorName = creator.ValueKind != JsonValueKind.Undefined && creator.TryGetProperty("nick", out var nick) ? nick.GetString() : "Unknown";
        // creatorUrl is usually /users/nick
        var creatorUrl = creator.ValueKind != JsonValueKind.Undefined && creator.TryGetProperty("shortUrl", out var sUrl) ? sUrl.GetString() : "";

        // Construct cleaner SourceUrl. Format: https://cults3d.com/en/3d-model/various/{slug}
        // Note: category is needed but 'various' often acts as catch-all or we guess 'tool'/'art'.
        // If we don't have category, we can try without or use a generic one.
        var sourceUrl = !string.IsNullOrEmpty(slug) 
            ? $"https://cults3d.com/en/3d-model/various/{slug}" 
            : $"https://cults3d.com";

        return new ModelDto
        {
            ExternalId = externalId,
            Source = "Cults3D",
            Title = name ?? "Untitled",
            Description = description ?? "", 
            DescriptionHtml = description, 
            SourceUrl = sourceUrl,
            ThumbnailUrl = thumbUrl,
            CreatorName = creatorName ?? "Unknown",
            CreatorProfileUrl = creatorUrl ?? "",
            Price = price,
            Currency = "EUR",
            IsFree = price == 0,

            LikeCount = item.TryGetProperty("likesCount", out var lc) ? lc.GetInt32() : 0,
            CreatedAtSource = item.TryGetProperty("publishedAt", out var pa) ? pa.GetDateTime() : DateTime.MinValue,
            Tags = [], 
            ImageUrls = [thumbUrl]
        };
    }

    [GeneratedRegex(@"<script type=""application/ld\+json"">(.*?)</script>", RegexOptions.Singleline)]
    private static partial Regex JsonLdRegex();

    [GeneratedRegex(@"<h1[^>]*>(.*?)</h1>", RegexOptions.Singleline)]
    private static partial Regex TitleRegex();

    [GeneratedRegex(@"## 3D model description\n(.*?)\n\n##", RegexOptions.Singleline)]
    private static partial Regex DescriptionHeaderRegex();

    [GeneratedRegex(@"class=""[^""]*read-more-text[^""]*""[^>]*>(.*?)</div>", RegexOptions.Singleline)]
    private static partial Regex DescriptionBoxRegex();

    [GeneratedRegex(@"property=""og:image"" content=""([^""]+)""", RegexOptions.Singleline)]
    private static partial Regex OgImageRegex();

    [GeneratedRegex(@"class=""t-secondary""[^>]*>@(.*?)</a>", RegexOptions.Singleline)]
    private static partial Regex CreatorRegex();

    [GeneratedRegex("<.*?>")]
    private static partial Regex HtmlTagRegex();
}
