using Microsoft.EntityFrameworkCore;
using Moq;
using PoTool.Api.Persistence;
using PoTool.Api.Persistence.Entities.Onboarding;
using PoTool.Api.Services.Onboarding;
using PoTool.Shared.Onboarding;

namespace PoTool.Tests.Unit.Services.Onboarding;

[TestClass]
public sealed class OnboardingCrudServiceTests
{
    private Mock<IOnboardingValidationService> _validationService = null!;
    private Mock<IOnboardingStatusService> _statusService = null!;

    [TestInitialize]
    public void Setup()
    {
        _validationService = new Mock<IOnboardingValidationService>(MockBehavior.Strict);
        _statusService = new Mock<IOnboardingStatusService>(MockBehavior.Strict);
        _statusService
            .Setup(service => service.GetStatusAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(OnboardingOperationResult<OnboardingStatusDto>.Success(new OnboardingStatusDto(
                OnboardingConfigurationStatus.NotConfigured,
                OnboardingConfigurationStatus.NotConfigured,
                OnboardingConfigurationStatus.NotConfigured,
                OnboardingConfigurationStatus.NotConfigured,
                Array.Empty<OnboardingStatusIssueDto>(),
                Array.Empty<OnboardingStatusIssueDto>(),
                new OnboardingStatusCountsDto(0, 0, 0, 0, 0, 0, 0, 0, 0, 0))));
    }

    [TestMethod]
    public async Task CreateProjectAsync_WithValidRequest_PersistsEntity()
    {
        await using var dbContext = CreateDbContext();
        var connection = CreateConnection();
        dbContext.OnboardingTfsConnections.Add(connection);
        await dbContext.SaveChangesAsync();

        _validationService
            .Setup(service => service.ValidateProjectSourceAsync(
                It.IsAny<TfsConnection>(),
                It.Is<ProjectSource>(project => project.ProjectExternalId == "project-1" && project.TfsConnectionId == connection.Id),
                It.IsAny<CancellationToken>(),
                null,
                null))
            .ReturnsAsync(OnboardingOperationResult<ProjectSourceValidationResultDto>.Success(CreateProjectValidation("project-1", "Project One", "Description")));

        var service = CreateService(dbContext);
        var result = await service.CreateProjectAsync(new CreateProjectSourceRequest(connection.Id, "project-1"), CancellationToken.None);

        Assert.IsTrue(result.Succeeded);
        Assert.AreEqual("project-1", result.Data!.ProjectExternalId);
        Assert.AreEqual(1, await dbContext.OnboardingProjectSources.CountAsync());
        Assert.AreEqual("Project One", (await dbContext.OnboardingProjectSources.SingleAsync()).Snapshot.Name);
    }

    [TestMethod]
    public async Task CreateProjectAsync_WhenValidationFails_DoesNotPersist()
    {
        await using var dbContext = CreateDbContext();
        var connection = CreateConnection();
        dbContext.OnboardingTfsConnections.Add(connection);
        await dbContext.SaveChangesAsync();

        _validationService
            .Setup(service => service.ValidateProjectSourceAsync(
                It.IsAny<TfsConnection>(),
                It.IsAny<ProjectSource>(),
                It.IsAny<CancellationToken>(),
                null,
                null))
            .ReturnsAsync(OnboardingOperationResult<ProjectSourceValidationResultDto>.Failure(new OnboardingErrorDto(OnboardingErrorCode.ValidationFailed, "Invalid", null, false)));

        var service = CreateService(dbContext);
        var result = await service.CreateProjectAsync(new CreateProjectSourceRequest(connection.Id, "project-1"), CancellationToken.None);

        Assert.IsFalse(result.Succeeded);
        Assert.AreEqual(OnboardingErrorCode.ValidationFailed, result.Error!.Code);
        Assert.AreEqual(0, await dbContext.OnboardingProjectSources.CountAsync());
    }

    [TestMethod]
    public async Task UpdateProjectAsync_WithAllowedFields_UpdatesAndRevalidates()
    {
        await using var dbContext = CreateDbContext();
        var connection = CreateConnection();
        dbContext.OnboardingTfsConnections.Add(connection);
        await dbContext.SaveChangesAsync();

        var project = CreateProject(connection.Id, "project-1");
        dbContext.OnboardingProjectSources.Add(project);
        await dbContext.SaveChangesAsync();

        _validationService
            .Setup(service => service.ValidateProjectSourceAsync(
                It.IsAny<TfsConnection>(),
                It.Is<ProjectSource>(item => item.Id == project.Id && item.Enabled == false),
                It.IsAny<CancellationToken>(),
                null,
                null))
            .ReturnsAsync(OnboardingOperationResult<ProjectSourceValidationResultDto>.Success(CreateProjectValidation("project-1", "Validated Name", "Validated Description")));

        var service = CreateService(dbContext);
        var result = await service.UpdateProjectAsync(project.Id, new UpdateProjectSourceRequest(false, "Display Name", "Display Description", null, null), CancellationToken.None);

        Assert.IsTrue(result.Succeeded);
        var persisted = await dbContext.OnboardingProjectSources.SingleAsync();
        Assert.IsFalse(persisted.Enabled);
        Assert.AreEqual("Display Name", persisted.Snapshot.Name);
        Assert.AreEqual("Display Description", persisted.Snapshot.Description);
    }

    [TestMethod]
    public async Task UpdateProjectAsync_WithForbiddenIdentityFields_IsRejected()
    {
        await using var dbContext = CreateDbContext();
        var connection = CreateConnection();
        dbContext.OnboardingTfsConnections.Add(connection);
        await dbContext.SaveChangesAsync();

        var project = CreateProject(connection.Id, "project-1");
        dbContext.OnboardingProjectSources.Add(project);
        await dbContext.SaveChangesAsync();

        var service = CreateService(dbContext);
        var result = await service.UpdateProjectAsync(project.Id, new UpdateProjectSourceRequest(null, null, null, null, "project-2"), CancellationToken.None);

        Assert.IsFalse(result.Succeeded);
        Assert.AreEqual(OnboardingErrorCode.ValidationFailed, result.Error!.Code);
        _validationService.VerifyNoOtherCalls();
    }

    [TestMethod]
    public async Task DeleteProjectAsync_WithActiveDependencies_IsBlocked()
    {
        await using var dbContext = CreateDbContext();
        var connection = CreateConnection();
        dbContext.OnboardingTfsConnections.Add(connection);
        await dbContext.SaveChangesAsync();

        var project = CreateProject(connection.Id, "project-1");
        dbContext.OnboardingProjectSources.Add(project);
        await dbContext.SaveChangesAsync();

        dbContext.OnboardingTeamSources.Add(CreateTeam(project.Id, "project-1", "team-1"));
        await dbContext.SaveChangesAsync();

        var service = CreateService(dbContext);
        var result = await service.DeleteProjectAsync(project.Id, new OnboardingSoftDeleteRequest("cleanup"), CancellationToken.None);

        Assert.IsFalse(result.Succeeded);
        Assert.AreEqual(OnboardingErrorCode.DependencyViolation, result.Error!.Code);
        Assert.IsFalse((await dbContext.OnboardingProjectSources.SingleAsync()).IsDeleted);
    }

    [TestMethod]
    public async Task WriteOperations_AlwaysTriggerValidation()
    {
        await using var dbContext = CreateDbContext();
        var connection = CreateConnection();
        dbContext.OnboardingTfsConnections.Add(connection);
        await dbContext.SaveChangesAsync();

        var project = CreateProject(connection.Id, "project-1");
        dbContext.OnboardingProjectSources.Add(project);
        await dbContext.SaveChangesAsync();

        _validationService
            .Setup(service => service.ValidateProjectSourceAsync(It.IsAny<TfsConnection>(), It.Is<ProjectSource>(item => item.ProjectExternalId == "project-2"), It.IsAny<CancellationToken>(), null, null))
            .ReturnsAsync(OnboardingOperationResult<ProjectSourceValidationResultDto>.Success(CreateProjectValidation("project-2", "Project Two", null)));
        _validationService
            .Setup(service => service.ValidateProjectSourceAsync(It.IsAny<TfsConnection>(), It.Is<ProjectSource>(item => item.Id == project.Id), It.IsAny<CancellationToken>(), null, null))
            .ReturnsAsync(OnboardingOperationResult<ProjectSourceValidationResultDto>.Success(CreateProjectValidation("project-1", "Project One", null)));

        var service = CreateService(dbContext);
        await service.CreateProjectAsync(new CreateProjectSourceRequest(connection.Id, "project-2"), CancellationToken.None);
        await service.UpdateProjectAsync(project.Id, new UpdateProjectSourceRequest(true, null, null, null, null), CancellationToken.None);

        _validationService.Verify(service => service.ValidateProjectSourceAsync(It.IsAny<TfsConnection>(), It.IsAny<ProjectSource>(), It.IsAny<CancellationToken>(), null, null), Times.Exactly(2));
    }

    [TestMethod]
    public async Task CreateTeamAsync_WithoutProject_IsRejectedAsOrphan()
    {
        await using var dbContext = CreateDbContext();
        var service = CreateService(dbContext);

        var result = await service.CreateTeamAsync(new CreateTeamSourceRequest(123, "team-1"), CancellationToken.None);

        Assert.IsFalse(result.Succeeded);
        Assert.AreEqual(OnboardingErrorCode.DependencyViolation, result.Error!.Code);
        Assert.AreEqual(0, await dbContext.OnboardingTeamSources.CountAsync());
    }

    [TestMethod]
    public async Task CreateBindingAsync_WithBrokenProjectScope_IsRejected()
    {
        await using var dbContext = CreateDbContext();
        var connection = CreateConnection();
        dbContext.OnboardingTfsConnections.Add(connection);
        await dbContext.SaveChangesAsync();

        var projectOne = CreateProject(connection.Id, "project-1");
        var projectTwo = CreateProject(connection.Id, "project-2");
        dbContext.OnboardingProjectSources.AddRange(projectOne, projectTwo);
        await dbContext.SaveChangesAsync();

        var root = CreateRoot(projectOne.Id, "project-1", "root-1");
        var team = CreateTeam(projectTwo.Id, "project-2", "team-2");
        dbContext.OnboardingProductRoots.Add(root);
        dbContext.OnboardingTeamSources.Add(team);
        await dbContext.SaveChangesAsync();

        var service = CreateService(dbContext);
        var result = await service.CreateBindingAsync(new CreateProductSourceBindingRequest(root.Id, OnboardingProductSourceTypeDto.Team, TeamSourceId: team.Id), CancellationToken.None);

        Assert.IsFalse(result.Succeeded);
        Assert.AreEqual(OnboardingErrorCode.ValidationFailed, result.Error!.Code);
        Assert.AreEqual(0, await dbContext.OnboardingProductSourceBindings.CountAsync());
    }

    [TestMethod]
    public async Task DeleteBindingAsync_SoftDeletesAndRecordsReason()
    {
        await using var dbContext = CreateDbContext();
        var connection = CreateConnection();
        dbContext.OnboardingTfsConnections.Add(connection);
        await dbContext.SaveChangesAsync();

        var project = CreateProject(connection.Id, "project-1");
        dbContext.OnboardingProjectSources.Add(project);
        await dbContext.SaveChangesAsync();

        var root = CreateRoot(project.Id, "project-1", "root-1");
        dbContext.OnboardingProductRoots.Add(root);
        await dbContext.SaveChangesAsync();

        var binding = CreateBinding();
        binding.ProductRootId = root.Id;
        binding.ProjectSourceId = project.Id;
        dbContext.OnboardingProductSourceBindings.Add(binding);
        await dbContext.SaveChangesAsync();

        var service = CreateService(dbContext);
        var result = await service.DeleteBindingAsync(binding.Id, new OnboardingSoftDeleteRequest("duplicate"), CancellationToken.None);

        Assert.IsTrue(result.Succeeded);
        var persisted = await dbContext.OnboardingProductSourceBindings.SingleAsync();
        Assert.IsTrue(persisted.IsDeleted);
        Assert.AreEqual("duplicate", persisted.DeletionReason);
    }

    private OnboardingCrudService CreateService(PoToolDbContext dbContext)
        => new(dbContext, _validationService.Object, _statusService.Object);

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
            AvailabilityValidationState = CreateValidationState(),
            PermissionValidationState = CreateValidationState(),
            CapabilityValidationState = CreateValidationState(),
            LastAttemptedValidationAtUtc = DateTime.UtcNow,
            LastSuccessfulValidationAtUtc = DateTime.UtcNow
        };

    private static ProjectSource CreateProject(int connectionId, string projectExternalId)
        => new()
        {
            TfsConnectionId = connectionId,
            ProjectExternalId = projectExternalId,
            Enabled = true,
            Snapshot = new ProjectSnapshot
            {
                ProjectExternalId = projectExternalId,
                Name = projectExternalId,
                Metadata = CreateMetadata()
            },
            ValidationState = CreateValidationState()
        };

    private static TeamSource CreateTeam(int projectId, string projectExternalId, string teamExternalId)
        => new()
        {
            ProjectSourceId = projectId,
            TeamExternalId = teamExternalId,
            Enabled = true,
            Snapshot = new TeamSnapshot
            {
                TeamExternalId = teamExternalId,
                ProjectExternalId = projectExternalId,
                Name = teamExternalId,
                DefaultAreaPath = "Project\\Team",
                Metadata = CreateMetadata()
            },
            ValidationState = CreateValidationState()
        };

    private static ProductRoot CreateRoot(int projectId, string projectExternalId, string workItemExternalId)
        => new()
        {
            ProjectSourceId = projectId,
            WorkItemExternalId = workItemExternalId,
            Enabled = true,
            Snapshot = new ProductRootSnapshot
            {
                WorkItemExternalId = workItemExternalId,
                Title = workItemExternalId,
                WorkItemType = "Epic",
                State = "New",
                ProjectExternalId = projectExternalId,
                AreaPath = "Project",
                Metadata = CreateMetadata()
            },
            ValidationState = CreateValidationState()
        };

    private static ProductSourceBinding CreateBinding()
        => new()
        {
            ProductRootId = 1,
            ProjectSourceId = 1,
            SourceType = ProductSourceType.Project,
            SourceExternalId = "project-1",
            Enabled = true,
            ValidationState = CreateValidationState()
        };

    private static OnboardingValidationState CreateValidationState()
        => new()
        {
            Status = OnboardingValidationStatus.Valid.ToString(),
            ValidatedAtUtc = DateTime.UtcNow
        };

    private static OnboardingSnapshotMetadata CreateMetadata()
        => new()
        {
            ConfirmedAtUtc = DateTime.UtcNow,
            LastSeenAtUtc = DateTime.UtcNow,
            IsCurrent = true,
            RenameDetected = false
        };

    private static ProjectSourceValidationResultDto CreateProjectValidation(string projectExternalId, string name, string? description)
        => new(projectExternalId, new ProjectSnapshotDto(projectExternalId, name, description, new SnapshotMetadataDto(DateTime.UtcNow, DateTime.UtcNow, true, false, null)), new OnboardingValidationStateDto(OnboardingValidationStatus.Valid, DateTime.UtcNow, OnboardingValidationSource.Live, null, null, Array.Empty<string>(), null, null, null));
}
