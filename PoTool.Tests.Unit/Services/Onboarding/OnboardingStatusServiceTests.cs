using Microsoft.EntityFrameworkCore;
using Moq;
using PoTool.Api.Persistence;
using PoTool.Api.Persistence.Entities.Onboarding;
using PoTool.Api.Services.Onboarding;
using PoTool.Shared.Onboarding;

namespace PoTool.Tests.Unit.Services.Onboarding;

[TestClass]
public sealed class OnboardingStatusServiceTests
{
    [TestMethod]
    public async Task GetStatusAsync_WithNoPersistedEntities_ReturnsNotConfigured()
    {
        await using var dbContext = CreateDbContext();
        var service = CreateService(dbContext);

        var result = await service.GetStatusAsync(CancellationToken.None);

        Assert.IsTrue(result.Succeeded);
        Assert.AreEqual(OnboardingConfigurationStatus.NotConfigured, result.Data!.OverallStatus);
        Assert.AreEqual(OnboardingConfigurationStatus.NotConfigured, result.Data.ConnectionStatus);
        Assert.AreEqual(OnboardingConfigurationStatus.NotConfigured, result.Data.DataSourceSetupStatus);
        Assert.AreEqual(OnboardingConfigurationStatus.NotConfigured, result.Data.DomainConfigurationStatus);
        CollectionAssert.AreEquivalent(
            new[] { "CONNECTION_REQUIRED", "PROJECT_SOURCE_REQUIRED", "PRODUCT_ROOT_REQUIRED" },
            result.Data.BlockingReasons.Select(item => item.Code).ToArray());
    }

    [TestMethod]
    public async Task GetStatusAsync_WithOnlyValidatedConnection_ReturnsPartiallyConfigured()
    {
        await using var dbContext = CreateDbContext();
        dbContext.OnboardingTfsConnections.Add(CreateConnection());
        await dbContext.SaveChangesAsync();

        var service = CreateService(dbContext);

        var result = await service.GetStatusAsync(CancellationToken.None);

        Assert.IsTrue(result.Succeeded);
        Assert.AreEqual(OnboardingConfigurationStatus.PartiallyConfigured, result.Data!.OverallStatus);
        Assert.AreEqual(OnboardingConfigurationStatus.Complete, result.Data.ConnectionStatus);
        Assert.AreEqual(OnboardingConfigurationStatus.NotConfigured, result.Data.DataSourceSetupStatus);
        Assert.AreEqual(OnboardingConfigurationStatus.NotConfigured, result.Data.DomainConfigurationStatus);
        CollectionAssert.Contains(result.Data.BlockingReasons.Select(item => item.Code).ToList(), "PROJECT_SOURCE_REQUIRED");
        CollectionAssert.Contains(result.Data.BlockingReasons.Select(item => item.Code).ToList(), "PRODUCT_ROOT_REQUIRED");
    }

    [TestMethod]
    public async Task GetStatusAsync_WithCompleteConfiguration_ReturnsCompleteAndWarnings()
    {
        await using var dbContext = CreateDbContext();
        var connection = CreateConnection();
        dbContext.OnboardingTfsConnections.Add(connection);
        await dbContext.SaveChangesAsync();

        var project = CreateProjectSource(connection.Id, "project-1");
        project.Snapshot.Metadata.RenameDetected = true;
        dbContext.OnboardingProjectSources.Add(project);
        await dbContext.SaveChangesAsync();

        var team = CreateTeamSource(project.Id, project.ProjectExternalId, "team-1");
        var pipeline = CreatePipelineSource(project.Id, project.ProjectExternalId, "pipeline-1");
        var root = CreateProductRoot(project.Id, project.ProjectExternalId, "root-1");
        dbContext.OnboardingTeamSources.Add(team);
        dbContext.OnboardingPipelineSources.Add(pipeline);
        dbContext.OnboardingProductRoots.Add(root);
        await dbContext.SaveChangesAsync();

        dbContext.OnboardingProductSourceBindings.AddRange(
            CreateProjectBinding(root.Id, project.Id, project.ProjectExternalId),
            CreateTeamBinding(root.Id, project.Id, team.Id, team.TeamExternalId),
            CreatePipelineBinding(root.Id, project.Id, pipeline.Id, pipeline.PipelineExternalId));
        await dbContext.SaveChangesAsync();

        var service = CreateService(dbContext);

        var result = await service.GetStatusAsync(CancellationToken.None);

        Assert.IsTrue(result.Succeeded);
        Assert.AreEqual(OnboardingConfigurationStatus.Complete, result.Data!.OverallStatus);
        Assert.AreEqual(OnboardingConfigurationStatus.Complete, result.Data.ConnectionStatus);
        Assert.AreEqual(OnboardingConfigurationStatus.Complete, result.Data.DataSourceSetupStatus);
        Assert.AreEqual(OnboardingConfigurationStatus.Complete, result.Data.DomainConfigurationStatus);
        Assert.AreEqual(1, result.Data.Counts.ProjectSourcesTotal);
        Assert.AreEqual(1, result.Data.Counts.ProjectSourcesValid);
        Assert.AreEqual(1, result.Data.Counts.TeamSourcesValid);
        Assert.AreEqual(1, result.Data.Counts.PipelineSourcesValid);
        Assert.AreEqual(1, result.Data.Counts.ProductRootsValid);
        Assert.AreEqual(3, result.Data.Counts.BindingsValid);
        CollectionAssert.Contains(result.Data.Warnings.Select(item => item.Code).ToList(), "SNAPSHOT_RENAME_DETECTED");
        Assert.IsEmpty(result.Data.BlockingReasons);
    }

    [TestMethod]
    public async Task GetStatusAsync_WithMissingProjectBindingForTeamBinding_ReturnsBlockingPartialStatus()
    {
        await using var dbContext = CreateDbContext();
        var connection = CreateConnection();
        dbContext.OnboardingTfsConnections.Add(connection);
        await dbContext.SaveChangesAsync();

        var project = CreateProjectSource(connection.Id, "project-1");
        dbContext.OnboardingProjectSources.Add(project);
        await dbContext.SaveChangesAsync();

        var team = CreateTeamSource(project.Id, project.ProjectExternalId, "team-1");
        var root = CreateProductRoot(project.Id, project.ProjectExternalId, "root-1");
        dbContext.OnboardingTeamSources.Add(team);
        dbContext.OnboardingProductRoots.Add(root);
        await dbContext.SaveChangesAsync();

        dbContext.OnboardingProductSourceBindings.Add(CreateTeamBinding(root.Id, project.Id, team.Id, team.TeamExternalId));
        await dbContext.SaveChangesAsync();

        var service = CreateService(dbContext);

        var result = await service.GetStatusAsync(CancellationToken.None);

        Assert.IsTrue(result.Succeeded);
        Assert.AreEqual(OnboardingConfigurationStatus.PartiallyConfigured, result.Data!.OverallStatus);
        Assert.AreEqual(OnboardingConfigurationStatus.Complete, result.Data.DataSourceSetupStatus);
        Assert.AreEqual(OnboardingConfigurationStatus.PartiallyConfigured, result.Data.DomainConfigurationStatus);
        CollectionAssert.Contains(result.Data.BlockingReasons.Select(item => item.Code).ToList(), "PRODUCT_ROOT_PROJECT_BINDING_REQUIRED");
        CollectionAssert.Contains(result.Data.BlockingReasons.Select(item => item.Code).ToList(), "TEAM_BINDING_PROJECT_BINDING_REQUIRED");
    }

    [TestMethod]
    public async Task GetStatusAsync_WithInvalidEnabledProject_DegradesFromComplete()
    {
        await using var dbContext = CreateDbContext();
        var connection = CreateConnection();
        dbContext.OnboardingTfsConnections.Add(connection);
        await dbContext.SaveChangesAsync();

        var validProject = CreateProjectSource(connection.Id, "project-1");
        var invalidProject = CreateProjectSource(connection.Id, "project-2");
        invalidProject.ValidationState = CreateValidationState(OnboardingValidationStatus.Invalid);
        dbContext.OnboardingProjectSources.AddRange(validProject, invalidProject);
        await dbContext.SaveChangesAsync();

        var root = CreateProductRoot(validProject.Id, validProject.ProjectExternalId, "root-1");
        dbContext.OnboardingProductRoots.Add(root);
        await dbContext.SaveChangesAsync();

        dbContext.OnboardingProductSourceBindings.Add(CreateProjectBinding(root.Id, validProject.Id, validProject.ProjectExternalId));
        await dbContext.SaveChangesAsync();

        var service = CreateService(dbContext);

        var result = await service.GetStatusAsync(CancellationToken.None);

        Assert.IsTrue(result.Succeeded);
        Assert.AreEqual(OnboardingConfigurationStatus.PartiallyConfigured, result.Data!.OverallStatus);
        Assert.AreEqual(OnboardingConfigurationStatus.PartiallyConfigured, result.Data.DataSourceSetupStatus);
        CollectionAssert.Contains(result.Data.BlockingReasons.Select(item => item.Code).ToList(), "PROJECT_SOURCE_INVALID");
    }

    private static OnboardingStatusService CreateService(PoToolDbContext dbContext)
        => new(dbContext, Mock.Of<IOnboardingObservability>());

    private static PoToolDbContext CreateDbContext()
    {
        var options = new DbContextOptionsBuilder<PoToolDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString("N"))
            .Options;

        return new PoToolDbContext(options);
    }

    private static TfsConnection CreateConnection()
        => new()
        {
            ConnectionKey = "connection",
            OrganizationUrl = "https://dev.azure.com/example",
            AuthenticationMode = "Ntlm",
            TimeoutSeconds = 30,
            ApiVersion = "7.1",
            AvailabilityValidationState = CreateValidationState(OnboardingValidationStatus.Valid),
            PermissionValidationState = CreateValidationState(OnboardingValidationStatus.Valid),
            CapabilityValidationState = CreateValidationState(OnboardingValidationStatus.Valid),
            LastAttemptedValidationAtUtc = DateTime.UtcNow,
            LastSuccessfulValidationAtUtc = DateTime.UtcNow
        };

    private static ProjectSource CreateProjectSource(int connectionId, string projectExternalId)
        => new()
        {
            TfsConnectionId = connectionId,
            ProjectExternalId = projectExternalId,
            Enabled = true,
            Snapshot = new ProjectSnapshot
            {
                ProjectExternalId = projectExternalId,
                Name = $"Project {projectExternalId}",
                Metadata = CreateSnapshotMetadata()
            },
            ValidationState = CreateValidationState(OnboardingValidationStatus.Valid)
        };

    private static TeamSource CreateTeamSource(int projectSourceId, string projectExternalId, string teamExternalId)
        => new()
        {
            ProjectSourceId = projectSourceId,
            TeamExternalId = teamExternalId,
            Enabled = true,
            Snapshot = new TeamSnapshot
            {
                TeamExternalId = teamExternalId,
                ProjectExternalId = projectExternalId,
                Name = $"Team {teamExternalId}",
                DefaultAreaPath = "Project\\Team",
                Metadata = CreateSnapshotMetadata()
            },
            ValidationState = CreateValidationState(OnboardingValidationStatus.Valid)
        };

    private static PipelineSource CreatePipelineSource(int projectSourceId, string projectExternalId, string pipelineExternalId)
        => new()
        {
            ProjectSourceId = projectSourceId,
            PipelineExternalId = pipelineExternalId,
            Enabled = true,
            Snapshot = new PipelineSnapshot
            {
                PipelineExternalId = pipelineExternalId,
                ProjectExternalId = projectExternalId,
                Name = $"Pipeline {pipelineExternalId}",
                Metadata = CreateSnapshotMetadata()
            },
            ValidationState = CreateValidationState(OnboardingValidationStatus.Valid)
        };

    private static ProductRoot CreateProductRoot(int projectSourceId, string projectExternalId, string workItemExternalId)
        => new()
        {
            ProjectSourceId = projectSourceId,
            WorkItemExternalId = workItemExternalId,
            Enabled = true,
            Snapshot = new ProductRootSnapshot
            {
                WorkItemExternalId = workItemExternalId,
                Title = $"Root {workItemExternalId}",
                WorkItemType = "Epic",
                State = "New",
                ProjectExternalId = projectExternalId,
                AreaPath = "Project\\Area",
                Metadata = CreateSnapshotMetadata()
            },
            ValidationState = CreateValidationState(OnboardingValidationStatus.Valid)
        };

    private static ProductSourceBinding CreateProjectBinding(int rootId, int projectId, string sourceExternalId)
        => new()
        {
            ProductRootId = rootId,
            ProjectSourceId = projectId,
            SourceType = ProductSourceType.Project,
            SourceExternalId = sourceExternalId,
            Enabled = true,
            ValidationState = CreateValidationState(OnboardingValidationStatus.Valid)
        };

    private static ProductSourceBinding CreateTeamBinding(int rootId, int projectId, int teamId, string sourceExternalId)
        => new()
        {
            ProductRootId = rootId,
            ProjectSourceId = projectId,
            TeamSourceId = teamId,
            SourceType = ProductSourceType.Team,
            SourceExternalId = sourceExternalId,
            Enabled = true,
            ValidationState = CreateValidationState(OnboardingValidationStatus.Valid)
        };

    private static ProductSourceBinding CreatePipelineBinding(int rootId, int projectId, int pipelineId, string sourceExternalId)
        => new()
        {
            ProductRootId = rootId,
            ProjectSourceId = projectId,
            PipelineSourceId = pipelineId,
            SourceType = ProductSourceType.Pipeline,
            SourceExternalId = sourceExternalId,
            Enabled = true,
            ValidationState = CreateValidationState(OnboardingValidationStatus.Valid)
        };

    private static OnboardingValidationState CreateValidationState(OnboardingValidationStatus status)
        => new()
        {
            Status = status.ToString(),
            ValidatedAtUtc = DateTime.UtcNow
        };

    private static OnboardingSnapshotMetadata CreateSnapshotMetadata()
        => new()
        {
            ConfirmedAtUtc = DateTime.UtcNow,
            LastSeenAtUtc = DateTime.UtcNow,
            IsCurrent = true
        };
}
