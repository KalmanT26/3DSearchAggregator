using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using ModelAggregator.Api.Data;
using ModelAggregator.Api.Data.Entities;
using ModelAggregator.Api.DTOs;

namespace ModelAggregator.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class CollectionsController(AppDbContext db) : ControllerBase
{
    private Guid GetUserId() =>
        Guid.Parse(User.FindFirst(ClaimTypes.NameIdentifier)!.Value);

    /// <summary>
    /// List all collections for the authenticated user.
    /// </summary>
    [HttpGet]
    public async Task<ActionResult<List<CollectionSummaryDto>>> GetMyCollections()
    {
        var userId = GetUserId();

        var collections = await db.Collections
            .Where(c => c.UserId == userId)
            .OrderByDescending(c => c.UpdatedAt)
            .Select(c => new CollectionSummaryDto
            {
                Id = c.Id,
                Name = c.Name,
                Description = c.Description,
                IsPublic = c.IsPublic,
                ItemCount = c.Items.Count,
                CreatedAt = c.CreatedAt,
                UpdatedAt = c.UpdatedAt
            })
            .ToListAsync();

        return Ok(collections);
    }

    /// <summary>
    /// Create a new collection.
    /// </summary>
    [HttpPost]
    public async Task<ActionResult<CollectionSummaryDto>> CreateCollection([FromBody] CreateCollectionRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.Name))
            return BadRequest("Collection name is required");

        var userId = GetUserId();

        var collection = new Collection
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            Name = request.Name.Trim(),
            Description = request.Description?.Trim(),
            IsPublic = request.IsPublic,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        db.Collections.Add(collection);
        await db.SaveChangesAsync();

        return Created($"/api/collections/{collection.Id}", new CollectionSummaryDto
        {
            Id = collection.Id,
            Name = collection.Name,
            Description = collection.Description,
            IsPublic = collection.IsPublic,
            ItemCount = 0,
            CreatedAt = collection.CreatedAt,
            UpdatedAt = collection.UpdatedAt
        });
    }

    /// <summary>
    /// Get a collection with all its items. Public collections are visible to everyone.
    /// </summary>
    [HttpGet("{id:guid}")]
    [AllowAnonymous]
    public async Task<ActionResult<CollectionDetailDto>> GetCollection(Guid id)
    {
        var collection = await db.Collections
            .Include(c => c.Items.OrderByDescending(i => i.AddedAt))
            .Include(c => c.User)
            .FirstOrDefaultAsync(c => c.Id == id);

        if (collection == null)
            return NotFound("Collection not found");

        // Check access: must be public OR owned by the current user
        var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        var isOwner = userIdClaim != null && Guid.TryParse(userIdClaim, out var userId) && collection.UserId == userId;

        if (!collection.IsPublic && !isOwner)
            return NotFound("Collection not found");

        return Ok(new CollectionDetailDto
        {
            Id = collection.Id,
            Name = collection.Name,
            Description = collection.Description,
            IsPublic = collection.IsPublic,
            IsOwner = isOwner,
            OwnerName = collection.User.DisplayName,
            CreatedAt = collection.CreatedAt,
            UpdatedAt = collection.UpdatedAt,
            Items = collection.Items.Select(i => new CollectionItemDto
            {
                Id = i.Id,
                Source = i.Source,
                ExternalId = i.ExternalId,
                Title = i.Title,
                ThumbnailUrl = i.ThumbnailUrl,
                SourceUrl = i.SourceUrl,
                AddedAt = i.AddedAt
            }).ToList()
        });
    }

    /// <summary>
    /// Update a collection (owner only).
    /// </summary>
    [HttpPut("{id:guid}")]
    public async Task<IActionResult> UpdateCollection(Guid id, [FromBody] UpdateCollectionRequest request)
    {
        var userId = GetUserId();

        var collection = await db.Collections.FirstOrDefaultAsync(c => c.Id == id && c.UserId == userId);
        if (collection == null)
            return NotFound("Collection not found");

        if (request.Name != null) collection.Name = request.Name.Trim();
        if (request.Description != null) collection.Description = request.Description.Trim();
        if (request.IsPublic.HasValue) collection.IsPublic = request.IsPublic.Value;
        collection.UpdatedAt = DateTime.UtcNow;

        await db.SaveChangesAsync();
        return NoContent();
    }

    /// <summary>
    /// Delete a collection and all its items (owner only).
    /// </summary>
    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> DeleteCollection(Guid id)
    {
        var userId = GetUserId();

        var collection = await db.Collections.FirstOrDefaultAsync(c => c.Id == id && c.UserId == userId);
        if (collection == null)
            return NotFound("Collection not found");

        db.Collections.Remove(collection);
        await db.SaveChangesAsync();
        return NoContent();
    }

    /// <summary>
    /// Add a model to a collection (owner only).
    /// </summary>
    [HttpPost("{id:guid}/items")]
    public async Task<ActionResult<CollectionItemDto>> AddItem(Guid id, [FromBody] AddItemRequest request)
    {
        var userId = GetUserId();

        var collection = await db.Collections.FirstOrDefaultAsync(c => c.Id == id && c.UserId == userId);
        if (collection == null)
            return NotFound("Collection not found");

        // Check for duplicate
        var exists = await db.CollectionItems.AnyAsync(ci =>
            ci.CollectionId == id && ci.Source == request.Source && ci.ExternalId == request.ExternalId);

        if (exists)
            return Conflict("Model is already in this collection");

        var item = new CollectionItem
        {
            Id = Guid.NewGuid(),
            CollectionId = id,
            Source = request.Source,
            ExternalId = request.ExternalId,
            Title = request.Title,
            ThumbnailUrl = request.ThumbnailUrl,
            SourceUrl = request.SourceUrl,
            AddedAt = DateTime.UtcNow
        };

        db.CollectionItems.Add(item);
        collection.UpdatedAt = DateTime.UtcNow;
        await db.SaveChangesAsync();

        return Created($"/api/collections/{id}/items/{item.Id}", new CollectionItemDto
        {
            Id = item.Id,
            Source = item.Source,
            ExternalId = item.ExternalId,
            Title = item.Title,
            ThumbnailUrl = item.ThumbnailUrl,
            SourceUrl = item.SourceUrl,
            AddedAt = item.AddedAt
        });
    }

    /// <summary>
    /// Remove a model from a collection (owner only).
    /// </summary>
    [HttpDelete("{id:guid}/items/{itemId:guid}")]
    public async Task<IActionResult> RemoveItem(Guid id, Guid itemId)
    {
        var userId = GetUserId();

        var collection = await db.Collections.FirstOrDefaultAsync(c => c.Id == id && c.UserId == userId);
        if (collection == null)
            return NotFound("Collection not found");

        var item = await db.CollectionItems.FirstOrDefaultAsync(ci => ci.Id == itemId && ci.CollectionId == id);
        if (item == null)
            return NotFound("Item not found");

        db.CollectionItems.Remove(item);
        collection.UpdatedAt = DateTime.UtcNow;
        await db.SaveChangesAsync();
        return NoContent();
    }
}
