using Microsoft.EntityFrameworkCore;
using PoTool.Api.Persistence.Entities;

namespace PoTool.Api.Persistence;

/// <summary>
/// EF Core database context for the PO Tool.
/// </summary>
public class PoToolDbContext : DbContext
{
    public PoToolDbContext(DbContextOptions<PoToolDbContext> options)
        : base(options)
    {
    }

    /// <summary>
    /// Work items cached from TFS.
    /// </summary>
    public DbSet<WorkItemEntity> WorkItems => Set<WorkItemEntity>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.Entity<WorkItemEntity>(entity =>
        {
            entity.HasIndex(e => e.TfsId)
                .IsUnique();
            
            entity.HasIndex(e => e.Type);
            
            entity.HasIndex(e => e.Title);
        });
    }
}
