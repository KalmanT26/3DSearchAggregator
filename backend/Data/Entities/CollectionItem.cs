namespace ModelAggregator.Api.Data.Entities;

public class CollectionItem
{
    public Guid Id { get; set; }
    public Guid CollectionId { get; set; }
    public Collection Collection { get; set; } = null!;

    /// <summary>Source platform name, e.g. "Thingiverse", "Cults3D"</summary>
    public string Source { get; set; } = "";

    /// <summary>ID of the model on the source platform</summary>
    public string ExternalId { get; set; } = "";

    public string Title { get; set; } = "";
    public string ThumbnailUrl { get; set; } = "";
    public string SourceUrl { get; set; } = "";
    public DateTime AddedAt { get; set; } = DateTime.UtcNow;
}
