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

    /// <summary>
    /// [DEPRECATED] Release Planning Board lanes (Objectives).
    /// Will be removed in a future version.
    /// </summary>
    public DbSet<LaneEntity> Lanes => Set<LaneEntity>();

    /// <summary>
    /// [DEPRECATED] Release Planning Board Epic placements.
    /// Replaced by PlanningEpicPlacements.
    /// </summary>
    public DbSet<EpicPlacementEntity> EpicPlacements => Set<EpicPlacementEntity>();

    /// <summary>
    /// [DEPRECATED] Release Planning Board milestone lines.
    /// Replaced by BoardRows with MarkerType=Release.
    /// </summary>
    public DbSet<MilestoneLineEntity> MilestoneLines => Set<MilestoneLineEntity>();

    /// <summary>
    /// [DEPRECATED] Release Planning Board iteration lines.
    /// Replaced by BoardRows with MarkerType=Iteration.
    /// </summary>
    public DbSet<IterationLineEntity> IterationLines => Set<IterationLineEntity>();

    /// <summary>
    /// Cached validation results for Release Planning Board Epics.
    /// </summary>
    public DbSet<CachedValidationResultEntity> CachedValidationResults => Set<CachedValidationResultEntity>();

    /// <summary>
    /// Planning Board rows (includes normal rows and marker rows).
    /// </summary>
    public DbSet<BoardRowEntity> BoardRows => Set<BoardRowEntity>();

    /// <summary>
    /// Planning Board Epic placements (new table-based board).
    /// </summary>
    public DbSet<PlanningEpicPlacementEntity> PlanningEpicPlacements => Set<PlanningEpicPlacementEntity>();

    /// <summary>
    /// Planning Board settings per Product Owner.
    /// </summary>
    public DbSet<PlanningBoardSettingsEntity> PlanningBoardSettings => Set<PlanningBoardSettingsEntity>();

    /// <summary>
    /// Products owned by Product Owners.
    /// </summary>
    public DbSet<ProductEntity> Products => Set<ProductEntity>();

    /// <summary>
    /// Teams that can work on products.
    /// </summary>
    public DbSet<TeamEntity> Teams => Set<TeamEntity>();

    /// <summary>
    /// Sprints (iterations) for teams.
    /// </summary>
    public DbSet<SprintEntity> Sprints => Set<SprintEntity>();

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

    /// <summary>
    /// Work item state classifications (project-wide configuration).
    /// </summary>
    public DbSet<WorkItemStateClassificationEntity> WorkItemStateClassifications => Set<WorkItemStateClassificationEntity>();

    /// <summary>
    /// Cache state tracking per ProductOwner.
    /// </summary>
    public DbSet<ProductOwnerCacheStateEntity> ProductOwnerCacheStates => Set<ProductOwnerCacheStateEntity>();

    /// <summary>
    /// Cached metrics per ProductOwner.
    /// </summary>
    public DbSet<CachedMetricsEntity> CachedMetrics => Set<CachedMetricsEntity>();

    /// <summary>
    /// Cached pipeline runs per ProductOwner.
    /// </summary>
    public DbSet<CachedPipelineRunEntity> CachedPipelineRuns => Set<CachedPipelineRunEntity>();

    /// <summary>
    /// Bug triage state tracking (local, not persisted to TFS).
    /// </summary>
    public DbSet<BugTriageStateEntity> BugTriageStates => Set<BugTriageStateEntity>();

    /// <summary>
    /// Triage tag catalog (configurable list of tags for Bugs Triage).
    /// </summary>
    public DbSet<TriageTagEntity> TriageTags => Set<TriageTagEntity>();

    /// <summary>
    /// Resolved hierarchical identifiers for work items.
    /// </summary>
    public DbSet<ResolvedWorkItemEntity> ResolvedWorkItems => Set<ResolvedWorkItemEntity>();

    /// <summary>
    /// Snapshot of current work item relationships for a ProductOwner.
    /// </summary>
    public DbSet<WorkItemRelationshipEdgeEntity> WorkItemRelationshipEdges => Set<WorkItemRelationshipEdgeEntity>();

    /// <summary>
    /// Append-only activity event ledger based on work item updates.
    /// </summary>
    public DbSet<ActivityEventLedgerEntryEntity> ActivityEventLedgerEntries => Set<ActivityEventLedgerEntryEntity>();

    /// <summary>
    /// Pre-computed sprint metrics projections.
    /// </summary>
    public DbSet<SprintMetricsProjectionEntity> SprintMetricsProjections => Set<SprintMetricsProjectionEntity>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.Entity<WorkItemEntity>(entity =>
        {
            entity.HasIndex(e => e.TfsId)
                .IsUnique();

            entity.HasIndex(e => e.Type);

            entity.HasIndex(e => e.Title);

            entity.HasIndex(e => e.TfsChangedDateUtc);
        });

        modelBuilder.Entity<TfsConfigEntity>(entity =>
        {
            entity.HasIndex(e => e.Url);
            entity.HasIndex(e => e.UpdatedAtUtc);
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

            entity.HasIndex(e => e.CreatedDateUtc);

            entity.HasOne(e => e.Product)
                .WithMany()
                .HasForeignKey(e => e.ProductId)
                .OnDelete(DeleteBehavior.SetNull);
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

            entity.HasIndex(e => e.CreatedDateUtc);
        });

        modelBuilder.Entity<PullRequestFileChangeEntity>(entity =>
        {
            entity.HasIndex(e => new { e.PullRequestId, e.IterationId });
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

        // New Planning Board entities
        modelBuilder.Entity<BoardRowEntity>(entity =>
        {
            entity.HasIndex(e => e.DisplayOrder);

            entity.Property(e => e.MarkerLabel).HasMaxLength(200);

            entity.HasMany(e => e.Placements)
                .WithOne(p => p.Row)
                .HasForeignKey(p => p.RowId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<PlanningEpicPlacementEntity>(entity =>
        {
            entity.HasIndex(e => e.EpicId)
                .IsUnique();

            entity.HasIndex(e => new { e.ProductId, e.RowId, e.OrderInCell });

            entity.HasOne(e => e.Product)
                .WithMany()
                .HasForeignKey(e => e.ProductId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasOne(e => e.Row)
                .WithMany(r => r.Placements)
                .HasForeignKey(e => e.RowId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<PlanningBoardSettingsEntity>(entity =>
        {
            entity.HasIndex(e => e.ProductOwnerId)
                .IsUnique();

            entity.Property(e => e.HiddenProductIdsJson).HasMaxLength(4000);

            entity.HasOne(e => e.ProductOwner)
                .WithOne()
                .HasForeignKey<PlanningBoardSettingsEntity>(e => e.ProductOwnerId)
                .OnDelete(DeleteBehavior.Cascade);
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
            entity.Property(e => e.ProjectName).HasMaxLength(256);
            entity.Property(e => e.TfsTeamId).HasMaxLength(100);
            entity.Property(e => e.TfsTeamName).HasMaxLength(200);
        });

        // Sprint entity
        modelBuilder.Entity<SprintEntity>(entity =>
        {
            entity.HasIndex(e => new { e.TeamId, e.Path })
                .IsUnique();

            entity.HasIndex(e => new { e.TeamId, e.StartDateUtc });

            entity.HasIndex(e => new { e.TeamId, e.LastSyncedDateUtc });

            entity.Property(e => e.TfsIterationId).HasMaxLength(100);
            entity.Property(e => e.Path).HasMaxLength(500).IsRequired();
            entity.Property(e => e.Name).HasMaxLength(200).IsRequired();
            entity.Property(e => e.TimeFrame).HasMaxLength(50);

            entity.HasOne(e => e.Team)
                .WithMany(t => t.Sprints)
                .HasForeignKey(e => e.TeamId)
                .OnDelete(DeleteBehavior.Cascade);
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

        // Work item state classifications
        modelBuilder.Entity<WorkItemStateClassificationEntity>(entity =>
        {
            entity.HasIndex(e => new { e.TfsProjectName, e.WorkItemType, e.StateName })
                .IsUnique();

            entity.Property(e => e.TfsProjectName).HasMaxLength(256).IsRequired();
            entity.Property(e => e.WorkItemType).HasMaxLength(100).IsRequired();
            entity.Property(e => e.StateName).HasMaxLength(100).IsRequired();
        });

        // ProductOwner cache state
        modelBuilder.Entity<ProductOwnerCacheStateEntity>(entity =>
        {
            entity.HasIndex(e => e.ProductOwnerId)
                .IsUnique();

            entity.Property(e => e.LastErrorMessage).HasMaxLength(2000);
            entity.Property(e => e.CurrentSyncStage).HasMaxLength(100);

            entity.HasOne(e => e.ProductOwner)
                .WithOne()
                .HasForeignKey<ProductOwnerCacheStateEntity>(e => e.ProductOwnerId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        // Cached metrics
        modelBuilder.Entity<CachedMetricsEntity>(entity =>
        {
            entity.HasIndex(e => new { e.ProductOwnerId, e.MetricName })
                .IsUnique();

            entity.Property(e => e.MetricName).HasMaxLength(100).IsRequired();
            entity.Property(e => e.Unit).HasMaxLength(50);

            entity.HasOne(e => e.ProductOwner)
                .WithMany()
                .HasForeignKey(e => e.ProductOwnerId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        // Cached pipeline runs
        modelBuilder.Entity<CachedPipelineRunEntity>(entity =>
        {
            entity.HasIndex(e => new { e.ProductOwnerId, e.PipelineDefinitionId, e.TfsRunId })
                .IsUnique();

            entity.HasIndex(e => e.FinishedDate);

            entity.Property(e => e.RunName).HasMaxLength(200);
            entity.Property(e => e.State).HasMaxLength(50);
            entity.Property(e => e.Result).HasMaxLength(50);
            entity.Property(e => e.SourceBranch).HasMaxLength(500);
            entity.Property(e => e.SourceVersion).HasMaxLength(50);
            entity.Property(e => e.Url).HasMaxLength(1000);

            entity.HasOne(e => e.ProductOwner)
                .WithMany()
                .HasForeignKey(e => e.ProductOwnerId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasOne(e => e.PipelineDefinition)
                .WithMany()
                .HasForeignKey(e => e.PipelineDefinitionId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        // Work item entity - add write-back field configurations
        modelBuilder.Entity<WorkItemEntity>(entity =>
        {
            // Existing configuration is in the base entity block above
            // Just adding the new field configurations here
            entity.Property(e => e.TfsETag).HasMaxLength(100);
        });

        // Bug triage state
        modelBuilder.Entity<BugTriageStateEntity>(entity =>
        {
            entity.HasIndex(e => e.BugId)
                .IsUnique();
        });

        // Triage tags
        modelBuilder.Entity<TriageTagEntity>(entity =>
        {
            entity.HasIndex(e => e.Name)
                .IsUnique();

            entity.HasIndex(e => e.DisplayOrder);
        });

        // Resolved work items
        modelBuilder.Entity<ResolvedWorkItemEntity>(entity =>
        {
            entity.HasIndex(e => e.WorkItemId)
                .IsUnique();

            entity.HasIndex(e => e.ResolvedProductId);
            entity.HasIndex(e => e.ResolvedEpicId);
            entity.HasIndex(e => e.ResolvedFeatureId);
            entity.HasIndex(e => e.ResolvedSprintId);
            entity.HasIndex(e => e.ResolutionStatus);

            entity.Property(e => e.WorkItemType).HasMaxLength(100).IsRequired();
        });

        modelBuilder.Entity<WorkItemRelationshipEdgeEntity>(entity =>
        {
            entity.HasIndex(e => new { e.ProductOwnerId, e.SourceWorkItemId });
            entity.HasIndex(e => new { e.ProductOwnerId, e.TargetWorkItemId });
            entity.Property(e => e.RelationType).HasMaxLength(256).IsRequired();
        });

        modelBuilder.Entity<ActivityEventLedgerEntryEntity>(entity =>
        {
            entity.HasIndex(e => new { e.ProductOwnerId, e.EventTimestamp });
            entity.HasIndex(e => new { e.WorkItemId, e.UpdateId });
            entity.HasIndex(e => new { e.WorkItemId, e.UpdateId, e.FieldRefName })
                .IsUnique();
            entity.Property(e => e.FieldRefName).HasMaxLength(256).IsRequired();
            entity.Property(e => e.IterationPath).HasMaxLength(500);
        });

        // Sprint metrics projections
        modelBuilder.Entity<SprintMetricsProjectionEntity>(entity =>
        {
            entity.HasIndex(e => new { e.SprintId, e.ProductId })
                .IsUnique();

            entity.HasIndex(e => e.SprintId);
            entity.HasIndex(e => e.ProductId);

            entity.HasOne(e => e.Sprint)
                .WithMany()
                .HasForeignKey(e => e.SprintId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasOne(e => e.Product)
                .WithMany()
                .HasForeignKey(e => e.ProductId)
                .OnDelete(DeleteBehavior.Cascade);
        });
    }
}
