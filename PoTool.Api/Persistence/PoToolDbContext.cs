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

    /// <summary>
    /// Persisted TFS configuration (non-sensitive data only).
    /// PAT is stored client-side using MAUI SecureStorage for security.
    /// See docs/PAT_STORAGE_BEST_PRACTICES.md
    /// </summary>
    public DbSet<TfsConfigEntity> TfsConfigs => Set<TfsConfigEntity>();

    /// <summary>
    /// Application settings.
    /// </summary>
    public DbSet<SettingsEntity> Settings => Set<SettingsEntity>();

    /// <summary>
    /// User profiles (area paths, team, goals).
    /// </summary>
    public DbSet<ProfileEntity> Profiles => Set<ProfileEntity>();

    /// <summary>
    /// Pull requests cached from TFS/Azure DevOps.
    /// </summary>
    public DbSet<PullRequestEntity> PullRequests => Set<PullRequestEntity>();

    /// <summary>
    /// Pull request iterations.
    /// </summary>
    public DbSet<PullRequestIterationEntity> PullRequestIterations => Set<PullRequestIterationEntity>();

    /// <summary>
    /// Pull request comments.
    /// </summary>
    public DbSet<PullRequestCommentEntity> PullRequestComments => Set<PullRequestCommentEntity>();

    /// <summary>
    /// Pull request file changes.
    /// </summary>
    public DbSet<PullRequestFileChangeEntity> PullRequestFileChanges => Set<PullRequestFileChangeEntity>();

    /// <summary>
    /// Effort estimation settings.
    /// </summary>
    public DbSet<EffortEstimationSettingsEntity> EffortEstimationSettings => Set<EffortEstimationSettingsEntity>();

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

        modelBuilder.Entity<TfsConfigEntity>(entity =>
        {
            entity.HasIndex(e => e.Url);
            // NOTE: ProtectedPat property removed - PAT stored client-side now
            entity.Property(e => e.Url).HasMaxLength(1024).IsRequired();
            entity.Property(e => e.Project).HasMaxLength(256);
        });

        modelBuilder.Entity<SettingsEntity>(entity =>
        {
            // Settings entity configuration (Id is primary key by convention)
        });

        modelBuilder.Entity<ProfileEntity>(entity =>
        {
            entity.HasIndex(e => e.Name);
            entity.Property(e => e.Name).HasMaxLength(200).IsRequired();
            entity.Property(e => e.AreaPaths).HasMaxLength(2000).IsRequired();
            entity.Property(e => e.TeamName).HasMaxLength(200).IsRequired();
            entity.Property(e => e.GoalIds).HasMaxLength(1000);
        });

        modelBuilder.Entity<PullRequestEntity>(entity =>
        {
            entity.HasIndex(e => e.Id)
                .IsUnique();
            
            entity.HasIndex(e => e.RepositoryName);
            
            entity.HasIndex(e => e.CreatedBy);
            
            entity.HasIndex(e => e.IterationPath);
            
            entity.HasIndex(e => e.Status);
        });

        modelBuilder.Entity<PullRequestIterationEntity>(entity =>
        {
            entity.HasIndex(e => new { e.PullRequestId, e.IterationNumber })
                .IsUnique();
        });

        modelBuilder.Entity<PullRequestCommentEntity>(entity =>
        {
            entity.HasIndex(e => e.PullRequestId);
            
            entity.HasIndex(e => e.ThreadId);
        });

        modelBuilder.Entity<PullRequestFileChangeEntity>(entity =>
        {
            entity.HasIndex(e => new { e.PullRequestId, e.IterationId });
        });

        modelBuilder.Entity<EffortEstimationSettingsEntity>(entity =>
        {
            // EffortEstimationSettings entity configuration (Id is primary key by convention)
        });
    }
}
