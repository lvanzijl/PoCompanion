using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using PoTool.Api.Persistence;
using PoTool.Api.Persistence.Entities.Onboarding;

namespace PoTool.Tests.Unit.Persistence;

[TestClass]
public sealed class OnboardingPersistenceFoundationTests
{
    [TestMethod]
    public async Task InsertValidOnboardingGraph_Succeeds()
    {
        await using var connection = await CreateOpenConnectionAsync();
        await using var context = await CreateContextAsync(connection);

        var utcNow = new DateTime(2026, 4, 5, 20, 0, 0, DateTimeKind.Utc);
        var tfsConnection = CreateConnection(utcNow);
        var projectSource = CreateProjectSource(utcNow);
        var teamSource = CreateTeamSource(utcNow);
        var pipelineSource = CreatePipelineSource(utcNow);
        var productRoot = CreateProductRoot(utcNow);

        tfsConnection.ProjectSources.Add(projectSource);
        projectSource.TeamSources.Add(teamSource);
        projectSource.PipelineSources.Add(pipelineSource);
        projectSource.ProductRoots.Add(productRoot);
        projectSource.ProductSourceBindings.Add(CreateProjectBinding(projectSource, productRoot, utcNow));
        teamSource.ProductSourceBindings.Add(CreateTeamBinding(projectSource, teamSource, productRoot, utcNow));
        pipelineSource.ProductSourceBindings.Add(CreatePipelineBinding(projectSource, pipelineSource, productRoot, utcNow));

        context.OnboardingTfsConnections.Add(tfsConnection);

        await context.SaveChangesAsync();

        Assert.AreEqual(1, await context.OnboardingTfsConnections.CountAsync());
        Assert.AreEqual(1, await context.OnboardingProjectSources.CountAsync());
        Assert.AreEqual(1, await context.OnboardingTeamSources.CountAsync());
        Assert.AreEqual(1, await context.OnboardingPipelineSources.CountAsync());
        Assert.AreEqual(1, await context.OnboardingProductRoots.CountAsync());
        Assert.AreEqual(3, await context.OnboardingProductSourceBindings.CountAsync());
    }

    [TestMethod]
    public async Task DuplicateProjectExternalIdWithinConnection_IsRejected()
    {
        await using var connection = await CreateOpenConnectionAsync();
        await using var context = await CreateContextAsync(connection);

        var utcNow = new DateTime(2026, 4, 5, 20, 10, 0, DateTimeKind.Utc);
        var tfsConnection = CreateConnection(utcNow);
        context.OnboardingTfsConnections.Add(tfsConnection);
        await context.SaveChangesAsync();

        context.OnboardingProjectSources.Add(CreateProjectSource(utcNow, tfsConnection.Id, "project-a"));
        await context.SaveChangesAsync();

        context.OnboardingProjectSources.Add(CreateProjectSource(utcNow, tfsConnection.Id, "project-a"));

        await Assert.ThrowsExactlyAsync<DbUpdateException>(() => context.SaveChangesAsync());
    }

    [TestMethod]
    public async Task DuplicateBindingCompositeIdentity_IsRejected()
    {
        await using var connection = await CreateOpenConnectionAsync();
        await using var context = await CreateContextAsync(connection);

        var utcNow = new DateTime(2026, 4, 5, 20, 20, 0, DateTimeKind.Utc);
        var graph = await SeedMinimalGraphAsync(context, utcNow);

        context.OnboardingProductSourceBindings.Add(CreateProjectBinding(graph.ProjectSource, graph.ProductRoot, utcNow));
        context.OnboardingProductSourceBindings.Add(CreateProjectBinding(graph.ProjectSource, graph.ProductRoot, utcNow));

        await Assert.ThrowsExactlyAsync<DbUpdateException>(() => context.SaveChangesAsync());
    }

    [TestMethod]
    public async Task MissingRequiredField_IsRejected()
    {
        await using var connection = await CreateOpenConnectionAsync();
        await using var context = await CreateContextAsync(connection);

        var utcNow = new DateTime(2026, 4, 5, 20, 30, 0, DateTimeKind.Utc);
        var tfsConnection = CreateConnection(utcNow);
        tfsConnection.OrganizationUrl = null!;

        context.OnboardingTfsConnections.Add(tfsConnection);

        await Assert.ThrowsExactlyAsync<DbUpdateException>(() => context.SaveChangesAsync());
    }

    [TestMethod]
    public async Task MissingRequiredParentReference_IsRejected()
    {
        await using var connection = await CreateOpenConnectionAsync();
        await using var context = await CreateContextAsync(connection);

        var utcNow = new DateTime(2026, 4, 5, 20, 40, 0, DateTimeKind.Utc);
        context.OnboardingTeamSources.Add(new TeamSource
        {
            ProjectSourceId = 999,
            TeamExternalId = "team-missing-parent",
            Enabled = true,
            Snapshot = CreateTeamSnapshot("team-missing-parent", "project-missing", utcNow),
            ValidationState = CreateValidationState(utcNow),
            CreatedAtUtc = utcNow,
            UpdatedAtUtc = utcNow
        });

        await Assert.ThrowsExactlyAsync<InvalidOperationException>(() => context.SaveChangesAsync());
    }

    [TestMethod]
    public async Task InvalidBindingSourcePattern_IsRejected()
    {
        await using var connection = await CreateOpenConnectionAsync();
        await using var context = await CreateContextAsync(connection);

        var utcNow = new DateTime(2026, 4, 5, 20, 50, 0, DateTimeKind.Utc);
        var graph = await SeedMinimalGraphAsync(context, utcNow);

        context.OnboardingProductSourceBindings.Add(new ProductSourceBinding
        {
            ProductRootId = graph.ProductRoot.Id,
            ProjectSourceId = graph.ProjectSource.Id,
            SourceType = ProductSourceType.Team,
            SourceExternalId = "team-missing-reference",
            Enabled = true,
            ValidationState = CreateValidationState(utcNow),
            CreatedAtUtc = utcNow,
            UpdatedAtUtc = utcNow
        });

        await Assert.ThrowsExactlyAsync<DbUpdateException>(() => context.SaveChangesAsync());
    }

    private static async Task<SqliteConnection> CreateOpenConnectionAsync()
    {
        var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        return connection;
    }

    private static async Task<PoToolDbContext> CreateContextAsync(SqliteConnection connection)
    {
        var options = new DbContextOptionsBuilder<PoToolDbContext>()
            .UseSqlite(connection)
            .Options;

        var context = new PoToolDbContext(options);
        await context.Database.EnsureCreatedAsync();
        return context;
    }

    private static TfsConnection CreateConnection(DateTime utcNow)
        => new()
        {
            ConnectionKey = "connection",
            OrganizationUrl = "https://dev.azure.com/example",
            AuthenticationMode = "Pat",
            TimeoutSeconds = 30,
            ApiVersion = "7.1",
            AvailabilityValidationState = CreateValidationState(utcNow),
            PermissionValidationState = CreateValidationState(utcNow),
            CapabilityValidationState = CreateValidationState(utcNow),
            LastSuccessfulValidationAtUtc = utcNow,
            LastAttemptedValidationAtUtc = utcNow,
            ValidationFailureReason = null,
            LastVerifiedCapabilitiesSummary = "Core onboarding capabilities confirmed",
            CreatedAtUtc = utcNow,
            UpdatedAtUtc = utcNow
        };

    private static ProjectSource CreateProjectSource(DateTime utcNow, int tfsConnectionId = 0, string projectExternalId = "project-a")
        => new()
        {
            TfsConnectionId = tfsConnectionId,
            ProjectExternalId = projectExternalId,
            Enabled = true,
            Snapshot = CreateProjectSnapshot(projectExternalId, utcNow),
            ValidationState = CreateValidationState(utcNow),
            CreatedAtUtc = utcNow,
            UpdatedAtUtc = utcNow
        };

    private static TeamSource CreateTeamSource(DateTime utcNow, int projectSourceId = 0, string teamExternalId = "team-a")
        => new()
        {
            ProjectSourceId = projectSourceId,
            TeamExternalId = teamExternalId,
            Enabled = true,
            Snapshot = CreateTeamSnapshot(teamExternalId, "project-a", utcNow),
            ValidationState = CreateValidationState(utcNow),
            CreatedAtUtc = utcNow,
            UpdatedAtUtc = utcNow
        };

    private static PipelineSource CreatePipelineSource(DateTime utcNow, int projectSourceId = 0, string pipelineExternalId = "pipeline-a")
        => new()
        {
            ProjectSourceId = projectSourceId,
            PipelineExternalId = pipelineExternalId,
            Enabled = true,
            Snapshot = CreatePipelineSnapshot(pipelineExternalId, "project-a", utcNow),
            ValidationState = CreateValidationState(utcNow),
            CreatedAtUtc = utcNow,
            UpdatedAtUtc = utcNow
        };

    private static ProductRoot CreateProductRoot(DateTime utcNow, int projectSourceId = 0, string workItemExternalId = "1001")
        => new()
        {
            ProjectSourceId = projectSourceId,
            WorkItemExternalId = workItemExternalId,
            Enabled = true,
            Snapshot = CreateProductRootSnapshot(workItemExternalId, "project-a", utcNow),
            ValidationState = CreateValidationState(utcNow),
            CreatedAtUtc = utcNow,
            UpdatedAtUtc = utcNow
        };

    private static ProductSourceBinding CreateProjectBinding(ProjectSource projectSource, ProductRoot productRoot, DateTime utcNow)
        => new()
        {
            ProjectSource = projectSource,
            ProductRoot = productRoot,
            SourceType = ProductSourceType.Project,
            SourceExternalId = projectSource.ProjectExternalId,
            Enabled = true,
            ValidationState = CreateValidationState(utcNow),
            CreatedAtUtc = utcNow,
            UpdatedAtUtc = utcNow
        };

    private static ProductSourceBinding CreateTeamBinding(
        ProjectSource projectSource,
        TeamSource teamSource,
        ProductRoot productRoot,
        DateTime utcNow)
        => new()
        {
            ProjectSource = projectSource,
            TeamSource = teamSource,
            ProductRoot = productRoot,
            SourceType = ProductSourceType.Team,
            SourceExternalId = teamSource.TeamExternalId,
            Enabled = true,
            ValidationState = CreateValidationState(utcNow),
            CreatedAtUtc = utcNow,
            UpdatedAtUtc = utcNow
        };

    private static ProductSourceBinding CreatePipelineBinding(
        ProjectSource projectSource,
        PipelineSource pipelineSource,
        ProductRoot productRoot,
        DateTime utcNow)
        => new()
        {
            ProjectSource = projectSource,
            PipelineSource = pipelineSource,
            ProductRoot = productRoot,
            SourceType = ProductSourceType.Pipeline,
            SourceExternalId = pipelineSource.PipelineExternalId,
            Enabled = true,
            ValidationState = CreateValidationState(utcNow),
            CreatedAtUtc = utcNow,
            UpdatedAtUtc = utcNow
        };

    private static ProjectSnapshot CreateProjectSnapshot(string projectExternalId, DateTime utcNow)
        => new()
        {
            ProjectExternalId = projectExternalId,
            Name = "Project Alpha",
            Description = "Primary onboarding project",
            Metadata = CreateSnapshotMetadata(utcNow)
        };

    private static TeamSnapshot CreateTeamSnapshot(string teamExternalId, string projectExternalId, DateTime utcNow)
        => new()
        {
            TeamExternalId = teamExternalId,
            ProjectExternalId = projectExternalId,
            Name = "Team Alpha",
            DefaultAreaPath = "Project Alpha\\Team Alpha",
            Description = "Primary delivery team",
            Metadata = CreateSnapshotMetadata(utcNow)
        };

    private static PipelineSnapshot CreatePipelineSnapshot(string pipelineExternalId, string projectExternalId, DateTime utcNow)
        => new()
        {
            PipelineExternalId = pipelineExternalId,
            ProjectExternalId = projectExternalId,
            Name = "Build Alpha",
            Folder = "\\Primary",
            YamlPath = "/pipelines/build-alpha.yml",
            RepositoryExternalId = "repo-123",
            RepositoryName = "PlatformRepo",
            Metadata = CreateSnapshotMetadata(utcNow)
        };

    private static ProductRootSnapshot CreateProductRootSnapshot(string workItemExternalId, string projectExternalId, DateTime utcNow)
        => new()
        {
            WorkItemExternalId = workItemExternalId,
            Title = "Product Root",
            WorkItemType = "Epic",
            State = "Active",
            ProjectExternalId = projectExternalId,
            AreaPath = "Project Alpha\\Area",
            Metadata = CreateSnapshotMetadata(utcNow)
        };

    private static OnboardingSnapshotMetadata CreateSnapshotMetadata(DateTime utcNow)
        => new()
        {
            ConfirmedAtUtc = utcNow,
            LastSeenAtUtc = utcNow,
            IsCurrent = true,
            RenameDetected = false,
            StaleReason = null
        };

    private static OnboardingValidationState CreateValidationState(DateTime utcNow)
        => new()
        {
            Status = "Valid",
            ValidatedAtUtc = utcNow,
            ErrorCode = null,
            Message = null,
            IsRetryable = false
        };

    private static async Task<(ProjectSource ProjectSource, ProductRoot ProductRoot)> SeedMinimalGraphAsync(
        PoToolDbContext context,
        DateTime utcNow)
    {
        var tfsConnection = CreateConnection(utcNow);
        context.OnboardingTfsConnections.Add(tfsConnection);
        await context.SaveChangesAsync();

        var projectSource = CreateProjectSource(utcNow, tfsConnection.Id, "project-a");
        context.OnboardingProjectSources.Add(projectSource);
        await context.SaveChangesAsync();

        var productRoot = CreateProductRoot(utcNow, projectSource.Id, "1001");
        context.OnboardingProductRoots.Add(productRoot);
        await context.SaveChangesAsync();

        return (projectSource, productRoot);
    }
}
