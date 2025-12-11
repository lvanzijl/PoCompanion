using Api.Data.Entities;
using Microsoft.EntityFrameworkCore;

namespace Api.Data;

/// <summary>
/// EF Core database context for the application.
/// </summary>
public sealed class ApplicationDbContext : DbContext
{
    public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options)
        : base(options)
    {
    }

    /// <summary>
    /// Work items table.
    /// </summary>
    public DbSet<WorkItemEntity> WorkItems => Set<WorkItemEntity>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        // Configure WorkItemEntity
        modelBuilder.Entity<WorkItemEntity>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.HasIndex(e => e.TfsId).IsUnique();
            entity.HasIndex(e => e.AreaPath);
            entity.HasIndex(e => e.Type);
            entity.HasIndex(e => e.RetrievedAt);
        });
    }
}
