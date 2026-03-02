namespace ModelAggregator.Api.DTOs;

// --- Auth ---
public class GoogleLoginRequest
{
    public string IdToken { get; set; } = "";
}

public class RegisterRequest
{
    public string Email { get; set; } = "";
    public string Password { get; set; } = "";
    public string DisplayName { get; set; } = "";
}

public class LoginRequest
{
    public string Email { get; set; } = "";
    public string Password { get; set; } = "";
}

public class AuthResponse
{
    public string Token { get; set; } = "";
    public UserDto User { get; set; } = null!;
}

public class UserDto
{
    public Guid Id { get; set; }
    public string Email { get; set; } = "";
    public string DisplayName { get; set; } = "";
    public string? AvatarUrl { get; set; }
}

// --- Collections ---
public class CreateCollectionRequest
{
    public string Name { get; set; } = "";
    public string? Description { get; set; }
    public bool IsPublic { get; set; } = false;
}

public class UpdateCollectionRequest
{
    public string? Name { get; set; }
    public string? Description { get; set; }
    public bool? IsPublic { get; set; }
}

public class AddItemRequest
{
    public string Source { get; set; } = "";
    public string ExternalId { get; set; } = "";
    public string Title { get; set; } = "";
    public string ThumbnailUrl { get; set; } = "";
    public string SourceUrl { get; set; } = "";
}

public class CollectionSummaryDto
{
    public Guid Id { get; set; }
    public string Name { get; set; } = "";
    public string? Description { get; set; }
    public bool IsPublic { get; set; }
    public int ItemCount { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
}

public class CollectionDetailDto
{
    public Guid Id { get; set; }
    public string Name { get; set; } = "";
    public string? Description { get; set; }
    public bool IsPublic { get; set; }
    public bool IsOwner { get; set; }
    public string OwnerName { get; set; } = "";
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
    public List<CollectionItemDto> Items { get; set; } = new();
}

public class CollectionItemDto
{
    public Guid Id { get; set; }
    public string Source { get; set; } = "";
    public string ExternalId { get; set; } = "";
    public string Title { get; set; } = "";
    public string ThumbnailUrl { get; set; } = "";
    public string SourceUrl { get; set; } = "";
    public DateTime AddedAt { get; set; }
}
