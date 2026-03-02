namespace ModelAggregator.Api.Data.Entities;

public class User
{
    public Guid Id { get; set; }
    public string? GoogleId { get; set; } // Nullable because manual users won't have it initially
    public string Email { get; set; } = "";
    public string DisplayName { get; set; } = "";
    public string? PasswordHash { get; set; } // Nullable because Google users won't have it initially
    public string? AvatarUrl { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public ICollection<Collection> Collections { get; set; } = new List<Collection>();
}
