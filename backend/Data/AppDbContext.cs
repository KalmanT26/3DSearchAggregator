using Microsoft.EntityFrameworkCore;
using ModelAggregator.Api.Data.Entities;

namespace ModelAggregator.Api.Data;

public class AppDbContext(DbContextOptions<AppDbContext> options) : DbContext(options)
{
    public DbSet<User> Users => Set<User>();
    public DbSet<Collection> Collections => Set<Collection>();
    public DbSet<CollectionItem> CollectionItems => Set<CollectionItem>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        // --- User ---
        modelBuilder.Entity<User>(entity =>
        {
            entity.HasKey(u => u.Id);
            entity.HasIndex(u => u.GoogleId).IsUnique();
            entity.HasIndex(u => u.Email).IsUnique();
            entity.Property(u => u.GoogleId).HasMaxLength(128);
            entity.Property(u => u.Email).HasMaxLength(256);
            entity.Property(u => u.DisplayName).HasMaxLength(256);
            entity.Property(u => u.AvatarUrl).HasMaxLength(1024);
        });

        // --- Collection ---
        modelBuilder.Entity<Collection>(entity =>
        {
            entity.HasKey(c => c.Id);
            entity.Property(c => c.Name).HasMaxLength(200);
            entity.Property(c => c.Description).HasMaxLength(1000);

            entity.HasOne(c => c.User)
                  .WithMany(u => u.Collections)
                  .HasForeignKey(c => c.UserId)
                  .OnDelete(DeleteBehavior.Cascade);
        });

        // --- CollectionItem ---
        modelBuilder.Entity<CollectionItem>(entity =>
        {
            entity.HasKey(ci => ci.Id);
            entity.Property(ci => ci.Source).HasMaxLength(50);
            entity.Property(ci => ci.ExternalId).HasMaxLength(256);
            entity.Property(ci => ci.Title).HasMaxLength(500);
            entity.Property(ci => ci.ThumbnailUrl).HasMaxLength(2048);
            entity.Property(ci => ci.SourceUrl).HasMaxLength(2048);

            // Prevent saving the same model twice in a collection
            entity.HasIndex(ci => new { ci.CollectionId, ci.Source, ci.ExternalId }).IsUnique();

            entity.HasOne(ci => ci.Collection)
                  .WithMany(c => c.Items)
                  .HasForeignKey(ci => ci.CollectionId)
                  .OnDelete(DeleteBehavior.Cascade);
        });
    }
}
