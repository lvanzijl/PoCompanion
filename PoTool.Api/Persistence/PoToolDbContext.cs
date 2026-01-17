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
    /// Timeframe iterations (weekly time buckets for filtering PRs).
    /// </summary>
    public DbSet<TimeframeIterationEntity> TimeframeIterations => Set<TimeframeIterationEntity>();

    /// <summary>
    /// Effort estimation settings.
    /// </summary>
    public DbSet<EffortEstimationSettingsEntity> EffortEstimationSettings => Set<EffortEstimationSettingsEntity>();

    /// <summary>
    /// Release Planning Board lanes (Objectives).
    /// </summary>
    public DbSet<LaneEntity> Lanes => Set<LaneEntity>();

    /// <summary>
    /// Release Planning Board Epic placements.
    /// </summary>
    public DbSet<EpicPlacementEntity> EpicPlacements => Set<EpicPlacementEntity>();

    /// <summary>
    /// Release Planning Board milestone lines.
    /// </summary>
    public DbSet<MilestoneLineEntity> MilestoneLines => Set<MilestoneLineEntity>();

    /// <summary>
    /// Release Planning Board iteration lines.
    /// </summary>
    public DbSet<IterationLineEntity> IterationLines => Set<IterationLineEntity>();

    /// <summary>
    /// Cached validation results for Release Planning Board Epics.
    /// </summary>
    public DbSet<CachedValidationResultEntity> CachedValidationResults => Set<CachedValidationResultEntity>();

    /// <summary>
    /// Products owned by Product Owners.
    /// </summary>
    public DbSet<ProductEntity> Products => Set<ProductEntity>();

    /// <summary>
    /// Teams that can work on products.
    /// </summary>
    public DbSet<TeamEntity> Teams => Set<TeamEntity>();

    /// <summary>
    /// Product-Team many-to-many links.
    /// </summary>
    public DbSet<ProductTeamLinkEntity> ProductTeamLinks => Set<ProductTeamLinkEntity>();

    /// <summary>
    /// Repositories configured per product.
    /// </summary>
    public DbSet<RepositoryEntity> Repositories => Set<RepositoryEntity>();

    /// <summary>
    /// YAML pipeline definitions configured per repository.
    /// </summary>
    public DbSet<PipelineDefinitionEntity> PipelineDefinitions => Set<PipelineDefinitionEntity>();

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

            entity.HasIndex(e => e.ProductId);

            entity.HasIndex(e => e.TimeframeIterationId);

            entity.HasOne(e => e.Product)
                .WithMany()
                .HasForeignKey(e => e.ProductId)
                .OnDelete(DeleteBehavior.SetNull);

            entity.HasOne(e => e.TimeframeIteration)
                .WithMany()
                .HasForeignKey(e => e.TimeframeIterationId)
                .OnDelete(DeleteBehavior.Restrict);
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

        modelBuilder.Entity<TimeframeIterationEntity>(entity =>
        {
            entity.HasIndex(e => new { e.Year, e.WeekNumber })
                .IsUnique();
            
            entity.HasIndex(e => e.IterationKey)
                .IsUnique();
        });

        modelBuilder.Entity<EffortEstimationSettingsEntity>(entity =>
        {
            // EffortEstimationSettings entity configuration (Id is primary key by convention)
        });

        // Release Planning Board entities
        modelBuilder.Entity<LaneEntity>(entity =>
        {
            entity.HasIndex(e => e.ObjectiveId)
                .IsUnique();

            entity.HasMany(e => e.Placements)
                .WithOne(p => p.Lane)
                .HasForeignKey(p => p.LaneId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<EpicPlacementEntity>(entity =>
        {
            entity.HasIndex(e => e.EpicId)
                .IsUnique();

            entity.HasIndex(e => new { e.LaneId, e.RowIndex, e.OrderInRow });
        });

        modelBuilder.Entity<MilestoneLineEntity>(entity =>
        {
            entity.Property(e => e.Label).HasMaxLength(200).IsRequired();
        });

        modelBuilder.Entity<IterationLineEntity>(entity =>
        {
            entity.Property(e => e.Label).HasMaxLength(200).IsRequired();
        });

        modelBuilder.Entity<CachedValidationResultEntity>(entity =>
        {
            entity.HasIndex(e => e.EpicId)
                .IsUnique();
        });

        // Product entity
        modelBuilder.Entity<ProductEntity>(entity =>
        {
            entity.HasIndex(e => e.ProductOwnerId);
            entity.Property(e => e.Name).HasMaxLength(200).IsRequired();
            entity.Property(e => e.CustomPicturePath).HasMaxLength(512);

            entity.HasOne(e => e.ProductOwner)
                .WithMany(p => p.Products)
                .HasForeignKey(e => e.ProductOwnerId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        // Team entity
        modelBuilder.Entity<TeamEntity>(entity =>
        {
            entity.HasIndex(e => e.Name);
            entity.Property(e => e.Name).HasMaxLength(200).IsRequired();
            entity.Property(e => e.TeamAreaPath).HasMaxLength(500).IsRequired();
            entity.Property(e => e.CustomPicturePath).HasMaxLength(512);
        });

        // Product-Team many-to-many link
        modelBuilder.Entity<ProductTeamLinkEntity>(entity =>
        {
            entity.HasKey(e => new { e.ProductId, e.TeamId });

            entity.HasOne(e => e.Product)
                .WithMany(p => p.ProductTeamLinks)
                .HasForeignKey(e => e.ProductId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasOne(e => e.Team)
                .WithMany(t => t.ProductTeamLinks)
                .HasForeignKey(e => e.TeamId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        // Repository entity
        modelBuilder.Entity<RepositoryEntity>(entity =>
        {
            entity.HasIndex(e => new { e.ProductId, e.Name })
                .IsUnique();

            entity.Property(e => e.Name).HasMaxLength(200).IsRequired();

            entity.HasOne(e => e.Product)
                .WithMany(p => p.Repositories)
                .HasForeignKey(e => e.ProductId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        // Pipeline definition entity
        modelBuilder.Entity<PipelineDefinitionEntity>(entity =>
        {
            entity.HasIndex(e => new { e.ProductId, e.PipelineDefinitionId })
                .IsUnique();

            entity.HasIndex(e => e.RepositoryId);

            entity.Property(e => e.Name).HasMaxLength(200).IsRequired();
            entity.Property(e => e.RepoId).HasMaxLength(36).IsRequired();
            entity.Property(e => e.RepoName).HasMaxLength(200).IsRequired();
            entity.Property(e => e.YamlPath).HasMaxLength(500);
            entity.Property(e => e.Folder).HasMaxLength(500);
            entity.Property(e => e.Url).HasMaxLength(1000);

            entity.HasOne(e => e.Product)
                .WithMany()
                .HasForeignKey(e => e.ProductId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasOne(e => e.Repository)
                .WithMany()
                .HasForeignKey(e => e.RepositoryId)
                .OnDelete(DeleteBehavior.Cascade);
        });
    }
}
