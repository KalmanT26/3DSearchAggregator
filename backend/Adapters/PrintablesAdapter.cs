using System.Text;
using System.Text.Json;
using ModelAggregator.Api.DTOs;
using ModelAggregator.Api.Models;

namespace ModelAggregator.Api.Adapters;

public class PrintablesAdapter(HttpClient http, ILogger<PrintablesAdapter> logger) : IModelSourceAdapter
{
    private readonly HttpClient _http = http;
    private readonly ILogger<PrintablesAdapter> _logger = logger;
    private const string GraphQlEndpoint = "https://api.printables.com/graphql/";
    private const string MediaBaseUrl = "https://media.printables.com/";
    private const string SiteBaseUrl = "https://www.printables.com/model/";

    // Shared fragment for fields we need from search results
    private const string PrintFieldsFragment = @"
        id name slug likesCount downloadCount makesCount displayCount
        price premium datePublished
        image { filePath }
        user { publicUsername }
        category { name }
        license { name }
        tags { name }
    ";



    public ModelSource Source => ModelSource.Printables;

    public async Task<AdapterSearchResult> SearchAsync(string query, int page = 1, int pageSize = 20, string? sort = null, CancellationToken ct = default)
    {
        var result = new AdapterSearchResult { Source = "Printables" };
        var offset = (page - 1) * pageSize;

        // Map sort parameter to Printables ordering enum
        var ordering = sort switch
        {
            "newest" => "NEWEST",
            "likes" => "MOST_LIKED",
            "popular" => "MOST_DOWNLOADED",
            "price_asc" => "PRICE_LOW_TO_HIGH",
            "price_desc" => "PRICE_HIGH_TO_LOW",
            _ => (string?)null // Default relevance
        };

        var orderingArg = ordering != null ? $", ordering: {ordering}" : "";

        var gql = new
        {
            query = $@"query SearchPrints($q: String!, $limit: Int!, $offset: Int!) {{
                searchPrints2(query: $q, limit: $limit, offset: $offset{orderingArg}) {{
                    totalCount
                    items {{ {PrintFieldsFragment} }}
                }}
            }}",
            variables = new { q = query, limit = pageSize, offset }
        };

        try
        {
            var response = await PostGraphQlAsync(gql, ct);
            using var doc = await JsonDocument.ParseAsync(await response.Content.ReadAsStreamAsync(ct), cancellationToken: ct);
            var root = doc.RootElement;

            if (root.TryGetProperty("errors", out var errors))
            {
                _logger.LogError("Printables GraphQL errors: {Errors}", errors.ToString());
                return result;
            }

            if (!root.TryGetProperty("data", out var data) || !data.TryGetProperty("searchPrints2", out var searchResult))
            {
                _logger.LogWarning("Printables search returned no 'searchPrints2' block.");
                return result;
            }

            result.TotalCount = searchResult.TryGetProperty("totalCount", out var tc) ? tc.GetInt32() : 0;

            if (searchResult.TryGetProperty("items", out var items) && items.ValueKind == JsonValueKind.Array)
            {
                foreach (var item in items.EnumerateArray())
                {
                    result.Items.Add(MapToDto(item));
                }
            }

            _logger.LogInformation("Printables: Found {Count} results (total: {Total}) for '{Query}'", result.Items.Count, result.TotalCount, query);
            return result;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Printables search failed for '{Query}'", query);
            return result;
        }
    }

    public async Task<AdapterSearchResult> GetTrendingAsync(int page = 1, int pageSize = 20, CancellationToken ct = default)
    {
        var result = new AdapterSearchResult { Source = "Printables" };
        var offset = (page - 1) * pageSize;

        // Use 'prints' query with featured ordering for trending
        var gql = new
        {
            query = $@"query TrendingPrints($limit: Int!, $offset: Int!) {{
                prints(limit: $limit, offset: $offset, ordering: ""-likes_count_7_days"") {{
                    {PrintFieldsFragment}
                }}
                printsCount {{ }}
            }}",
            variables = new { limit = pageSize, offset }
        };

        try
        {
            var response = await PostGraphQlAsync(gql, ct);
            using var doc = await JsonDocument.ParseAsync(await response.Content.ReadAsStreamAsync(ct), cancellationToken: ct);
            var root = doc.RootElement;

            if (root.TryGetProperty("errors", out var errors))
            {
                _logger.LogError("Printables GraphQL trending errors: {Errors}", errors.ToString());
                // Fallback to a generic search if trending query fails
                return await SearchAsync("print", page, pageSize, "likes", ct);
            }

            if (!root.TryGetProperty("data", out var data) || !data.TryGetProperty("prints", out var prints))
            {
                _logger.LogWarning("Printables trending returned no 'prints' block, falling back to search.");
                return await SearchAsync("print", page, pageSize, "likes", ct);
            }

            if (prints.ValueKind == JsonValueKind.Array)
            {
                foreach (var item in prints.EnumerateArray())
                {
                    result.Items.Add(MapToDto(item));
                }
            }

            result.TotalCount = result.Items.Count;
            _logger.LogInformation("Printables: Found {Count} trending results", result.Items.Count);
            return result;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Printables trending failed, falling back to search.");
            return await SearchAsync("print", page, pageSize, "likes", ct);
        }
    }

    public async Task<ModelDto?> GetModelDetailsAsync(string externalId, CancellationToken ct = default)
    {
        var gql = new
        {
            query = $@"query PrintDetail($id: ID!) {{
                print(id: $id) {{
                    id name slug description summary
                    likesCount downloadCount makesCount displayCount
                    price premium datePublished firstPublish
                    image {{ filePath }}
                    images {{ filePath }}
                    user {{ publicUsername }}
                    category {{ name }}
                    license {{ name }}
                    tags {{ name }}
                }}
            }}",
            variables = new { id = externalId }
        };

        try
        {
            var response = await PostGraphQlAsync(gql, ct);
            using var doc = await JsonDocument.ParseAsync(await response.Content.ReadAsStreamAsync(ct), cancellationToken: ct);
            var root = doc.RootElement;

            if (root.TryGetProperty("errors", out var errors))
            {
                _logger.LogError("Printables GraphQL detail errors: {Errors}", errors.ToString());
                return null;
            }

            if (!root.TryGetProperty("data", out var data) || !data.TryGetProperty("print", out var print) || print.ValueKind == JsonValueKind.Null)
            {
                _logger.LogWarning("Printables detail returned no 'print' for ID {Id}", externalId);
                return null;
            }

            var dto = MapToDto(print);

            // Enhance with detail-specific fields
            if (print.TryGetProperty("description", out var desc) && desc.ValueKind == JsonValueKind.String)
            {
                dto.DescriptionHtml = desc.GetString();
                dto.Description = dto.DescriptionHtml ?? "";
            }
            else if (print.TryGetProperty("summary", out var summary) && summary.ValueKind == JsonValueKind.String)
            {
                dto.Description = summary.GetString() ?? "";
            }

            // Extract all images
            if (print.TryGetProperty("images", out var images) && images.ValueKind == JsonValueKind.Array)
            {
                dto.ImageUrls = [];
                foreach (var img in images.EnumerateArray())
                {
                    if (img.TryGetProperty("filePath", out var fp) && fp.ValueKind == JsonValueKind.String)
                    {
                        dto.ImageUrls.Add(MediaBaseUrl + fp.GetString());
                    }
                }
                if (dto.ImageUrls.Count > 0 && string.IsNullOrEmpty(dto.ThumbnailUrl))
                {
                    dto.ThumbnailUrl = dto.ImageUrls[0];
                }
            }

            return dto;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Printables details failed for {Id}", externalId);
            return null;
        }
    }

    private async Task<HttpResponseMessage> PostGraphQlAsync(object gql, CancellationToken ct)
    {
        var json = JsonSerializer.Serialize(gql);
        var content = new StringContent(json, Encoding.UTF8, "application/json");
        var response = await _http.PostAsync(GraphQlEndpoint, content, ct);
        response.EnsureSuccessStatusCode();
        return response;
    }

    private static ModelDto MapToDto(JsonElement item)
    {
        var id = item.TryGetProperty("id", out var idProp)
            ? (idProp.ValueKind == JsonValueKind.Number ? idProp.GetInt64().ToString() : idProp.GetString() ?? "0")
            : "0";

        var slug = item.TryGetProperty("slug", out var s) ? s.GetString() ?? "" : "";
        var name = item.TryGetProperty("name", out var n) ? n.GetString() ?? "Untitled" : "Untitled";

        // Image: filePath is relative like "media/prints/258431/images/..."
        var thumbUrl = "";
        if (item.TryGetProperty("image", out var image) && image.ValueKind == JsonValueKind.Object
            && image.TryGetProperty("filePath", out var fp) && fp.ValueKind == JsonValueKind.String)
        {
            thumbUrl = MediaBaseUrl + fp.GetString();
        }

        // Creator
        var creatorName = "Unknown";
        if (item.TryGetProperty("user", out var user) && user.ValueKind == JsonValueKind.Object
            && user.TryGetProperty("publicUsername", out var un) && un.ValueKind == JsonValueKind.String)
        {
            creatorName = un.GetString() ?? "Unknown";
        }

        // Price
        decimal price = 0;
        bool isPremium = false;
        if (item.TryGetProperty("price", out var priceEl) && priceEl.ValueKind == JsonValueKind.Number)
        {
            price = priceEl.GetDecimal();
        }
        if (item.TryGetProperty("premium", out var premEl) && premEl.ValueKind != JsonValueKind.Null)
        {
            isPremium = premEl.GetBoolean();
        }

        // Tags
        var tags = new List<string>();
        if (item.TryGetProperty("tags", out var tagsEl) && tagsEl.ValueKind == JsonValueKind.Array)
        {
            foreach (var tag in tagsEl.EnumerateArray())
            {
                if (tag.TryGetProperty("name", out var tn) && tn.ValueKind == JsonValueKind.String)
                {
                    tags.Add(tn.GetString()!);
                }
            }
        }

        // Category & License
        string? category = null;
        if (item.TryGetProperty("category", out var catEl) && catEl.ValueKind == JsonValueKind.Object
            && catEl.TryGetProperty("name", out var cn) && cn.ValueKind == JsonValueKind.String)
        {
            category = cn.GetString();
        }

        string? license = null;
        if (item.TryGetProperty("license", out var licEl) && licEl.ValueKind == JsonValueKind.Object
            && licEl.TryGetProperty("name", out var ln) && ln.ValueKind == JsonValueKind.String)
        {
            license = ln.GetString();
        }

        // Source URL: https://www.printables.com/model/{id}-{slug}
        var sourceUrl = !string.IsNullOrEmpty(slug)
            ? $"{SiteBaseUrl}{id}-{slug}"
            : $"{SiteBaseUrl}{id}";

        return new ModelDto
        {
            ExternalId = id,
            Source = "Printables",
            Title = name,
            SourceUrl = sourceUrl,
            ThumbnailUrl = thumbUrl,
            ImageUrls = string.IsNullOrEmpty(thumbUrl) ? [] : [thumbUrl],
            CreatorName = creatorName,
            CreatorProfileUrl = $"https://www.printables.com/@{creatorName}",
            Price = price,
            Currency = "USD",
            IsFree = price == 0 && !isPremium,
            IsSubscriptionGated = isPremium,
            LikeCount = item.TryGetProperty("likesCount", out var lc) ? lc.GetInt32() : 0,
            ViewCount = item.TryGetProperty("displayCount", out var dc) ? dc.GetInt32() : 0,
            MakeCount = item.TryGetProperty("makesCount", out var mc) ? mc.GetInt32() : 0,
            FileCount = item.TryGetProperty("downloadCount", out var dlc) ? dlc.GetInt32() : 0,
            Tags = tags,
            Category = category,
            License = license,
            CreatedAtSource = item.TryGetProperty("datePublished", out var dp) && dp.ValueKind == JsonValueKind.String
                ? (DateTime.TryParse(dp.GetString(), out var d) ? d : DateTime.UtcNow)
                : DateTime.UtcNow,
        };
    }
}
