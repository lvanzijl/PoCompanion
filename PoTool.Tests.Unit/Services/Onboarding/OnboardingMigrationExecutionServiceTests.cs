using System.IO;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using PoTool.Api.Persistence;
using PoTool.Api.Persistence.Entities;
using PoTool.Api.Persistence.Entities.Onboarding;
using PoTool.Api.Services.Onboarding;
using PoTool.Shared.Onboarding;
using PoTool.Shared.Settings;

namespace PoTool.Tests.Unit.Services.Onboarding;

[TestClass]
public sealed class OnboardingMigrationExecutionServiceTests
{
    [TestMethod]
    public async Task ExecuteAsync_WithFullyValidLegacyConfiguration_PersistsMigratedEntitiesAndSucceededRun()
    {
        await using var connection = await CreateOpenConnectionAsync();
        await using var context = await CreateContextAsync(connection);
        await SeedLegacyConfigurationAsync(context);

        var lookupClient = CreateLookupClient();
        var service = CreateService(context, lookupClient);

        var summary = await service.ExecuteAsync(
            new OnboardingMigrationExecutionRequest("2026-04-06-slice-5", "dev", "Manual", OnboardingMigrationExecutionMode.Live),
            CancellationToken.None);

        Assert.AreEqual(OnboardingMigrationRunStatus.Succeeded, summary.Status);
        Assert.AreEqual(6, summary.TotalUnitCount);
        Assert.IsTrue(summary.Units.All(unit => unit.Status is OnboardingMigrationUnitStatus.Succeeded or OnboardingMigrationUnitStatus.Skipped));
        Assert.AreEqual(1, await context.OnboardingTfsConnections.CountAsync());
        Assert.AreEqual(1, await context.OnboardingProjectSources.CountAsync());
        Assert.AreEqual(1, await context.OnboardingTeamSources.CountAsync());
        Assert.AreEqual(1, await context.OnboardingPipelineSources.CountAsync());
        Assert.AreEqual(1, await context.OnboardingProductRoots.CountAsync());
        Assert.AreEqual(3, await context.OnboardingProductSourceBindings.CountAsync());
        Assert.AreEqual(0, await context.OnboardingMigrationIssues.CountAsync());
    }

    [TestMethod]
    public async Task ExecuteAsync_WithMissingTeamExternalId_RecordsIssueAndPartiallySucceeds()
    {
        await using var connection = await CreateOpenConnectionAsync();
        await using var context = await CreateContextAsync(connection);
        await SeedLegacyConfigurationAsync(context, includeBrokenTeam: true);

        var lookupClient = CreateLookupClient();
        var service = CreateService(context, lookupClient);

        var summary = await service.ExecuteAsync(
            new OnboardingMigrationExecutionRequest("2026-04-06-slice-5", "dev", "Manual", OnboardingMigrationExecutionMode.Live),
            CancellationToken.None);

        Assert.AreEqual(OnboardingMigrationRunStatus.PartiallySucceeded, summary.Status);
        Assert.IsTrue(summary.Issues.Any(issue => issue.TargetEntityType == nameof(TeamSource) && issue.IsBlocking));
        Assert.AreEqual(1, await context.OnboardingTeamSources.CountAsync());
    }

    [TestMethod]
    public async Task ExecuteAsync_WithUnresolvableProjectDependency_DoesNotPersistTeam()
    {
        await using var connection = await CreateOpenConnectionAsync();
        await using var context = await CreateContextAsync(connection);
        await SeedLegacyConfigurationAsync(context, connectionProjectName: string.Empty, teamProjectName: "Unknown Project", includePipeline: false, includeRoot: false);

        var lookupClient = CreateLookupClient();
        var service = CreateService(context, lookupClient);

        var summary = await service.ExecuteAsync(
            new OnboardingMigrationExecutionRequest("2026-04-06-slice-5", "dev", "Manual", OnboardingMigrationExecutionMode.Live),
            CancellationToken.None);

        Assert.AreEqual(OnboardingMigrationRunStatus.PartiallySucceeded, summary.Status);
        Assert.AreEqual(0, await context.OnboardingProjectSources.CountAsync());
        Assert.AreEqual(0, await context.OnboardingTeamSources.CountAsync());
        Assert.IsTrue(summary.Issues.Any(issue => issue.IssueCategory == "DependencyViolation" && issue.TargetEntityType == nameof(TeamSource)));
    }

    [TestMethod]
    public async Task ExecuteAsync_WhenValidationFails_DoesNotWriteInvalidEntityAndRecordsIssue()
    {
        await using var connection = await CreateOpenConnectionAsync();
        await using var context = await CreateContextAsync(connection);
        await SeedLegacyConfigurationAsync(context);

        var lookupClient = CreateLookupClient();
        lookupClient.WorkItems["101"] = OnboardingOperationResult<WorkItemLookupResultDto>.Failure(
            new OnboardingErrorDto(OnboardingErrorCode.NotFound, "Root missing", "101", false));

        var service = CreateService(context, lookupClient);

        var summary = await service.ExecuteAsync(
            new OnboardingMigrationExecutionRequest("2026-04-06-slice-5", "dev", "Manual", OnboardingMigrationExecutionMode.Live),
            CancellationToken.None);

        Assert.AreEqual(0, await context.OnboardingProductRoots.CountAsync());
        Assert.IsTrue(summary.Issues.Any(issue => issue.TargetEntityType == nameof(ProductRoot) && issue.IssueCategory == OnboardingErrorCode.NotFound.ToString()));
    }

    [TestMethod]
    public async Task ExecuteAsync_RerunWithSameFingerprint_DoesNotDuplicateEntities()
    {
        await using var connection = await CreateOpenConnectionAsync();
        await using var context = await CreateContextAsync(connection);
        await SeedLegacyConfigurationAsync(context);

        var lookupClient = CreateLookupClient();
        var service = CreateService(context, lookupClient);

        await service.ExecuteAsync(
            new OnboardingMigrationExecutionRequest("2026-04-06-slice-5", "dev", "Manual", OnboardingMigrationExecutionMode.Live),
            CancellationToken.None);
        var rerunSummary = await service.ExecuteAsync(
            new OnboardingMigrationExecutionRequest("2026-04-06-slice-5", "dev", "Manual", OnboardingMigrationExecutionMode.Live),
            CancellationToken.None);

        Assert.AreEqual(OnboardingMigrationRunStatus.NoOp, rerunSummary.Status);
        Assert.IsTrue(rerunSummary.Units.All(unit => unit.Status == OnboardingMigrationUnitStatus.Skipped));
        Assert.IsTrue(rerunSummary.Issues.Any(issue => issue.IssueCategory == "NoOp" && issue.Severity == OnboardingMigrationIssueSeverity.Info));
        Assert.AreEqual(1, await context.OnboardingTfsConnections.CountAsync());
        Assert.AreEqual(1, await context.OnboardingProjectSources.CountAsync());
        Assert.AreEqual(1, await context.OnboardingTeamSources.CountAsync());
        Assert.AreEqual(1, await context.OnboardingPipelineSources.CountAsync());
        Assert.AreEqual(1, await context.OnboardingProductRoots.CountAsync());
        Assert.AreEqual(3, await context.OnboardingProductSourceBindings.CountAsync());
        Assert.AreEqual(2, await context.OnboardingMigrationRuns.CountAsync());

        var fingerprints = await context.OnboardingMigrationRuns
            .Select(run => run.SourceFingerprint)
            .Distinct()
            .CountAsync();

        Assert.AreEqual(1, fingerprints);
    }

    [TestMethod]
    public async Task ExecuteAsync_RerunAfterPartialFailure_DoesNotNoOpSameFingerprint()
    {
        await using var connection = await CreateOpenConnectionAsync();
        await using var context = await CreateContextAsync(connection);
        await SeedLegacyConfigurationAsync(context);

        var lookupClient = CreateLookupClient();
        lookupClient.WorkItems["101"] = OnboardingOperationResult<WorkItemLookupResultDto>.Failure(
            new OnboardingErrorDto(OnboardingErrorCode.TfsUnavailable, "Unavailable", "101", true));
        var service = CreateService(context, lookupClient);

        var failedSummary = await service.ExecuteAsync(
            new OnboardingMigrationExecutionRequest("2026-04-06-slice-5", "dev", "Manual", OnboardingMigrationExecutionMode.Live),
            CancellationToken.None);

        lookupClient.WorkItems["101"] = OnboardingOperationResult<WorkItemLookupResultDto>.Success(
            new WorkItemLookupResultDto("101", "Root", "Epic", "New", "project-alpha", "Alpha\\Area"));

        var recoveredSummary = await service.ExecuteAsync(
            new OnboardingMigrationExecutionRequest("2026-04-06-slice-5", "dev", "Manual", OnboardingMigrationExecutionMode.Live),
            CancellationToken.None);

        Assert.AreEqual(OnboardingMigrationRunStatus.PartiallySucceeded, failedSummary.Status);
        Assert.AreEqual(OnboardingMigrationRunStatus.Succeeded, recoveredSummary.Status);
        Assert.AreEqual(1, await context.OnboardingProductRoots.CountAsync());
        Assert.AreEqual(2, await context.OnboardingMigrationRuns.CountAsync());
        Assert.IsFalse(recoveredSummary.Issues.Any(issue => issue.IssueCategory == "NoOp"));
    }

    [TestMethod]
    public async Task ExecuteAsync_WithDuplicateTeamExternalIdsAcrossProjects_PersistsScopedTeamSources()
    {
        await using var connection = await CreateOpenConnectionAsync();
        await using var context = await CreateContextAsync(connection);
        await SeedDuplicateTeamAcrossProjectsAsync(context);

        var lookupClient = CreateLookupClient();
        lookupClient.Projects = OnboardingOperationResult<IReadOnlyList<ProjectLookupResultDto>>.Success(
        [
            new ProjectLookupResultDto("project-alpha", "Alpha", "Alpha project"),
            new ProjectLookupResultDto("project-beta", "Beta", "Beta project")
        ]);
        lookupClient.Teams["project-alpha"] = OnboardingOperationResult<IReadOnlyList<TeamLookupResultDto>>.Success(
        [
            new TeamLookupResultDto("shared-team", "project-alpha", "Team Alpha", null, "Alpha\\Area")
        ]);
        lookupClient.Teams["project-beta"] = OnboardingOperationResult<IReadOnlyList<TeamLookupResultDto>>.Success(
        [
            new TeamLookupResultDto("shared-team", "project-beta", "Team Beta", null, "Beta\\Area")
        ]);
        lookupClient.WorkItems["101"] = OnboardingOperationResult<WorkItemLookupResultDto>.Success(
            new WorkItemLookupResultDto("101", "Root Alpha", "Epic", "New", "project-alpha", "Alpha\\Area"));
        lookupClient.WorkItems["202"] = OnboardingOperationResult<WorkItemLookupResultDto>.Success(
            new WorkItemLookupResultDto("202", "Root Beta", "Epic", "New", "project-beta", "Beta\\Area"));

        var service = CreateService(context, lookupClient);

        var summary = await service.ExecuteAsync(
            new OnboardingMigrationExecutionRequest("2026-04-06-slice-5", "dev", "Manual", OnboardingMigrationExecutionMode.Live),
            CancellationToken.None);

        Assert.AreEqual(OnboardingMigrationRunStatus.Succeeded, summary.Status);
        Assert.AreEqual(2, await context.OnboardingProjectSources.CountAsync());
        Assert.AreEqual(2, await context.OnboardingTeamSources.CountAsync());
        Assert.AreEqual(2, await context.OnboardingTeamSources.CountAsync(team => team.TeamExternalId == "shared-team"));
    }

    [TestMethod]
    public async Task ExecuteAsync_WhenPipelineDiscoveryFails_RecordsDiscoveryIssue()
    {
        await using var connection = await CreateOpenConnectionAsync();
        await using var context = await CreateContextAsync(connection);
        await SeedLegacyConfigurationAsync(context);

        var lookupClient = CreateLookupClient();
        lookupClient.Pipelines["project-alpha"] = OnboardingOperationResult<IReadOnlyList<PipelineLookupResultDto>>.Failure(
            new OnboardingErrorDto(OnboardingErrorCode.TfsUnavailable, "Pipelines unavailable", "project-alpha", true));

        var service = CreateService(context, lookupClient);

        var summary = await service.ExecuteAsync(
            new OnboardingMigrationExecutionRequest("2026-04-06-slice-5", "dev", "Manual", OnboardingMigrationExecutionMode.Live),
            CancellationToken.None);

        Assert.AreEqual(OnboardingMigrationRunStatus.PartiallySucceeded, summary.Status);
        Assert.IsTrue(summary.Issues.Any(issue =>
            issue.SourceLegacyReference == "PipelineDiscovery:project-alpha"
            && issue.IssueCategory == OnboardingErrorCode.TfsUnavailable.ToString()));
    }

    [TestMethod]
    public async Task ExecuteAsync_WhenRootMigrationFails_BindingUnitRecordsDependencyIssue()
    {
        await using var connection = await CreateOpenConnectionAsync();
        await using var context = await CreateContextAsync(connection);
        await SeedLegacyConfigurationAsync(context);

        var lookupClient = CreateLookupClient();
        lookupClient.WorkItems["101"] = OnboardingOperationResult<WorkItemLookupResultDto>.Failure(
            new OnboardingErrorDto(OnboardingErrorCode.NotFound, "Root missing", "101", false));

        var service = CreateService(context, lookupClient);

        var summary = await service.ExecuteAsync(
            new OnboardingMigrationExecutionRequest("2026-04-06-slice-5", "dev", "Manual", OnboardingMigrationExecutionMode.Live),
            CancellationToken.None);

        Assert.IsTrue(summary.Issues.Any(issue =>
            issue.TargetEntityType == nameof(ProductSourceBinding)
            && issue.SanitizedMessage.Contains("product root", StringComparison.OrdinalIgnoreCase)));
    }

    [TestMethod]
    public async Task ExecuteAsync_DryRunAndLiveProduceEquivalentIssues()
    {
        await using var dryRunConnection = await CreateOpenConnectionAsync();
        await using var liveRunConnection = await CreateOpenConnectionAsync();
        await using var dryRunContext = await CreateContextAsync(dryRunConnection);
        await using var liveRunContext = await CreateContextAsync(liveRunConnection);
        await SeedLegacyConfigurationAsync(dryRunContext, includeBrokenTeam: true);
        await SeedLegacyConfigurationAsync(liveRunContext, includeBrokenTeam: true);

        var dryRunService = CreateService(dryRunContext, CreateLookupClient());
        var liveRunService = CreateService(liveRunContext, CreateLookupClient());

        var dryRunSummary = await dryRunService.ExecuteAsync(
            new OnboardingMigrationExecutionRequest("2026-04-06-slice-5", "dev", "Manual", OnboardingMigrationExecutionMode.DryRun),
            CancellationToken.None);
        var liveRunSummary = await liveRunService.ExecuteAsync(
            new OnboardingMigrationExecutionRequest("2026-04-06-slice-5", "dev", "Manual", OnboardingMigrationExecutionMode.Live),
            CancellationToken.None);

        var dryRunIssues = dryRunSummary.Issues
            .Select(issue => $"{issue.IssueType}|{issue.IssueCategory}|{issue.TargetEntityType}|{issue.TargetExternalIdentity}|{issue.SourceLegacyReference}|{issue.Severity}|{issue.IsBlocking}")
            .OrderBy(value => value)
            .ToArray();
        var liveRunIssues = liveRunSummary.Issues
            .Select(issue => $"{issue.IssueType}|{issue.IssueCategory}|{issue.TargetEntityType}|{issue.TargetExternalIdentity}|{issue.SourceLegacyReference}|{issue.Severity}|{issue.IsBlocking}")
            .OrderBy(value => value)
            .ToArray();

        CollectionAssert.AreEqual(dryRunIssues, liveRunIssues);
        Assert.AreEqual(dryRunSummary.ProcessedEntityCount, liveRunSummary.ProcessedEntityCount);
        Assert.AreEqual(dryRunSummary.FailedEntityCount, liveRunSummary.FailedEntityCount);
    }

    [TestMethod]
    public async Task ExecuteAsync_ConcurrentRunAttempt_ThrowsInvalidOperationException()
    {
        var databasePath = Path.Combine(Path.GetTempPath(), $"{Guid.NewGuid():N}.db");

        try
        {
            await using (var seedContext = await CreateFileContextAsync(databasePath))
            {
                await SeedLegacyConfigurationAsync(seedContext);
            }

            var runLock = new OnboardingMigrationRunLock();
            var blockingLookupClient = new BlockingOnboardingLiveLookupClient(CreateLookupClient());

            await using var firstContext = await CreateFileContextAsync(databasePath);
            await using var secondContext = await CreateFileContextAsync(databasePath);
            var firstService = CreateService(firstContext, blockingLookupClient, runLock);
            var secondService = CreateService(secondContext, blockingLookupClient, runLock);

            var firstRunTask = firstService.ExecuteAsync(
                new OnboardingMigrationExecutionRequest("2026-04-06-slice-5", "dev", "Manual", OnboardingMigrationExecutionMode.Live),
                CancellationToken.None);

            await blockingLookupClient.WaitUntilBlockedAsync();

            await AssertInvalidOperationExceptionAsync(() =>
                secondService.ExecuteAsync(
                    new OnboardingMigrationExecutionRequest("2026-04-06-slice-5", "dev", "Manual", OnboardingMigrationExecutionMode.Live),
                    CancellationToken.None));

            blockingLookupClient.Release();
            await firstRunTask;
        }
        finally
        {
            if (File.Exists(databasePath))
            {
                File.Delete(databasePath);
            }
        }
    }

    [TestMethod]
    public async Task ExecuteAsync_RerunWithChangedLegacyData_UpsertsExistingRecords()
    {
        await using var connection = await CreateOpenConnectionAsync();
        await using var context = await CreateContextAsync(connection);
        await SeedLegacyConfigurationAsync(context);

        var lookupClient = CreateLookupClient();
        var service = CreateService(context, lookupClient);

        await service.ExecuteAsync(
            new OnboardingMigrationExecutionRequest("2026-04-06-slice-5", "dev", "Manual", OnboardingMigrationExecutionMode.Live),
            CancellationToken.None);

        var config = await context.TfsConfigs.SingleAsync();
        config.TimeoutSeconds = 90;
        config.UpdatedAtUtc = DateTime.UtcNow.AddMinutes(5);
        await context.SaveChangesAsync();

        await service.ExecuteAsync(
            new OnboardingMigrationExecutionRequest("2026-04-06-slice-5", "dev", "Manual", OnboardingMigrationExecutionMode.Live),
            CancellationToken.None);

        Assert.AreEqual(1, await context.OnboardingTfsConnections.CountAsync());
        Assert.AreEqual(90, await context.OnboardingTfsConnections.Select(item => item.TimeoutSeconds).SingleAsync());
        Assert.AreEqual(2, await context.OnboardingMigrationRuns.CountAsync());

        var fingerprints = await context.OnboardingMigrationRuns
            .Select(run => run.SourceFingerprint)
            .Distinct()
            .CountAsync();

        Assert.AreEqual(2, fingerprints);
    }

    [TestMethod]
    public async Task ExecuteAsync_DryRunRecordsLedgerOnly()
    {
        await using var connection = await CreateOpenConnectionAsync();
        await using var context = await CreateContextAsync(connection);
        await SeedLegacyConfigurationAsync(context);

        var lookupClient = CreateLookupClient();
        var service = CreateService(context, lookupClient);

        var summary = await service.ExecuteAsync(
            new OnboardingMigrationExecutionRequest("2026-04-06-slice-5", "dev", "Manual", OnboardingMigrationExecutionMode.DryRun),
            CancellationToken.None);

        Assert.AreEqual(OnboardingMigrationRunStatus.Succeeded, summary.Status);
        Assert.AreEqual(0, await context.OnboardingTfsConnections.CountAsync());
        Assert.AreEqual(0, await context.OnboardingProjectSources.CountAsync());
        Assert.AreEqual(0, await context.OnboardingTeamSources.CountAsync());
        Assert.AreEqual(0, await context.OnboardingPipelineSources.CountAsync());
        Assert.AreEqual(0, await context.OnboardingProductRoots.CountAsync());
        Assert.AreEqual(0, await context.OnboardingProductSourceBindings.CountAsync());
        Assert.AreEqual(1, await context.OnboardingMigrationRuns.CountAsync());
        Assert.AreEqual(6, await context.OnboardingMigrationUnits.CountAsync());
    }

    [TestMethod]
    public async Task ExecuteAsync_ReplayWithFrozenResponses_LeavesPersistedStateUnchanged()
    {
        await using var connection = await CreateOpenConnectionAsync();
        await using var context = await CreateContextAsync(connection);
        await SeedLegacyConfigurationAsync(context);

        var frozenResponses = CreateLookupClient();
        var firstSummary = await CreateService(context, CloneLookupClient(frozenResponses)).ExecuteAsync(
            new OnboardingMigrationExecutionRequest("2026-04-06-slice-5-proof", "dev", "Manual", OnboardingMigrationExecutionMode.Live),
            CancellationToken.None);
        var persistedStateAfterFirstRun = await CaptureOnboardingStateAsync(context);

        var secondSummary = await CreateService(context, CloneLookupClient(frozenResponses)).ExecuteAsync(
            new OnboardingMigrationExecutionRequest("2026-04-06-slice-5-proof", "dev", "Manual", OnboardingMigrationExecutionMode.Live),
            CancellationToken.None);
        var persistedStateAfterSecondRun = await CaptureOnboardingStateAsync(context);

        CollectionAssert.AreEqual(persistedStateAfterFirstRun, persistedStateAfterSecondRun);
        Assert.AreEqual(OnboardingMigrationRunStatus.NoOp, secondSummary.Status);
        Assert.AreEqual(0, secondSummary.ProcessedEntityCount);
        Assert.AreEqual(0, secondSummary.FailedEntityCount);
        Assert.IsTrue(secondSummary.Issues.Any(issue => issue.IssueCategory == "NoOp" && issue.Severity == OnboardingMigrationIssueSeverity.Info));
        Assert.AreEqual(0, firstSummary.BlockingIssueCount);
    }

    [TestMethod]
    public async Task ExecuteAsync_ReplayWithRenamedProject_SurfacesDriftWithoutMutatingPersistedState()
    {
        await using var connection = await CreateOpenConnectionAsync();
        await using var context = await CreateContextAsync(connection);
        await SeedLegacyConfigurationAsync(context);

        var baselineResponses = CreateLookupClient();
        await CreateService(context, CloneLookupClient(baselineResponses)).ExecuteAsync(
            new OnboardingMigrationExecutionRequest("2026-04-06-slice-5-proof", "dev", "Manual", OnboardingMigrationExecutionMode.Live),
            CancellationToken.None);
        var persistedStateBeforeDrift = await CaptureOnboardingStateAsync(context);

        var driftedResponses = CloneLookupClient(baselineResponses);
        driftedResponses.Projects = OnboardingOperationResult<IReadOnlyList<ProjectLookupResultDto>>.Success(
        [
            new ProjectLookupResultDto("project-alpha", "Alpha Renamed", "Alpha project")
        ]);

        var driftSummary = await CreateService(context, driftedResponses).ExecuteAsync(
            new OnboardingMigrationExecutionRequest("2026-04-06-slice-5-proof", "dev", "Manual", OnboardingMigrationExecutionMode.Live),
            CancellationToken.None);
        var persistedStateAfterDrift = await CaptureOnboardingStateAsync(context);

        CollectionAssert.AreEqual(persistedStateBeforeDrift, persistedStateAfterDrift);
        Assert.AreNotEqual(OnboardingMigrationRunStatus.NoOp, driftSummary.Status);
        Assert.IsTrue(driftSummary.Issues.Any(issue =>
            issue.IssueType == "ReplayDrift"
            && issue.TargetEntityType == nameof(ProjectSource)
            && issue.Severity == OnboardingMigrationIssueSeverity.Warning));
    }

    [TestMethod]
    public async Task ExecuteAsync_ReplayWithUnavailableWorkItem_SurfacesBlockingDriftWithoutMutatingPersistedState()
    {
        await using var connection = await CreateOpenConnectionAsync();
        await using var context = await CreateContextAsync(connection);
        await SeedLegacyConfigurationAsync(context);

        var baselineResponses = CreateLookupClient();
        await CreateService(context, CloneLookupClient(baselineResponses)).ExecuteAsync(
            new OnboardingMigrationExecutionRequest("2026-04-06-slice-5-proof", "dev", "Manual", OnboardingMigrationExecutionMode.Live),
            CancellationToken.None);
        var persistedStateBeforeDrift = await CaptureOnboardingStateAsync(context);

        var driftedResponses = CloneLookupClient(baselineResponses);
        driftedResponses.WorkItems["101"] = OnboardingOperationResult<WorkItemLookupResultDto>.Failure(
            new OnboardingErrorDto(OnboardingErrorCode.TfsUnavailable, "Unavailable", "101", true));

        var driftSummary = await CreateService(context, driftedResponses).ExecuteAsync(
            new OnboardingMigrationExecutionRequest("2026-04-06-slice-5-proof", "dev", "Manual", OnboardingMigrationExecutionMode.Live),
            CancellationToken.None);
        var persistedStateAfterDrift = await CaptureOnboardingStateAsync(context);

        CollectionAssert.AreEqual(persistedStateBeforeDrift, persistedStateAfterDrift);
        Assert.AreNotEqual(OnboardingMigrationRunStatus.NoOp, driftSummary.Status);
        Assert.IsTrue(driftSummary.Issues.Any(issue =>
            issue.TargetEntityType == nameof(ProductRoot)
            && issue.IssueCategory == OnboardingErrorCode.TfsUnavailable.ToString()
            && issue.IsBlocking));
    }

    [TestMethod]
    public async Task ExecuteAsync_ReplayWithPermissionDenied_SurfacesBlockingDriftWithoutMutatingPersistedState()
    {
        await using var connection = await CreateOpenConnectionAsync();
        await using var context = await CreateContextAsync(connection);
        await SeedLegacyConfigurationAsync(context);

        var baselineResponses = CreateLookupClient();
        await CreateService(context, CloneLookupClient(baselineResponses)).ExecuteAsync(
            new OnboardingMigrationExecutionRequest("2026-04-06-slice-5-proof", "dev", "Manual", OnboardingMigrationExecutionMode.Live),
            CancellationToken.None);
        var persistedStateBeforeDrift = await CaptureOnboardingStateAsync(context);

        var driftedResponses = CloneLookupClient(baselineResponses);
        driftedResponses.Projects = OnboardingOperationResult<IReadOnlyList<ProjectLookupResultDto>>.Failure(
            new OnboardingErrorDto(OnboardingErrorCode.PermissionDenied, "Denied", "projects", false));

        var driftSummary = await CreateService(context, driftedResponses).ExecuteAsync(
            new OnboardingMigrationExecutionRequest("2026-04-06-slice-5-proof", "dev", "Manual", OnboardingMigrationExecutionMode.Live),
            CancellationToken.None);
        var persistedStateAfterDrift = await CaptureOnboardingStateAsync(context);

        CollectionAssert.AreEqual(persistedStateBeforeDrift, persistedStateAfterDrift);
        Assert.AreNotEqual(OnboardingMigrationRunStatus.NoOp, driftSummary.Status);
        Assert.IsTrue(driftSummary.Issues.Any(issue =>
            issue.TargetEntityType == nameof(TfsConnection)
            && issue.IssueCategory == OnboardingErrorCode.PermissionDenied.ToString()
            && issue.IsBlocking));
    }

    [TestMethod]
    public async Task ExecuteAsync_WithDuplicatePipelineExternalIdsAcrossProjects_RecordsIssueAndDoesNotPersistInvalidPipelines()
    {
        await using var connection = await CreateOpenConnectionAsync();
        await using var context = await CreateContextAsync(connection);
        await SeedDuplicatePipelineAcrossProjectsAsync(context);

        var lookupClient = CreateLookupClient();
        lookupClient.Projects = OnboardingOperationResult<IReadOnlyList<ProjectLookupResultDto>>.Success(
        [
            new ProjectLookupResultDto("project-alpha", "Alpha", "Alpha project"),
            new ProjectLookupResultDto("project-beta", "Beta", "Beta project")
        ]);
        lookupClient.Pipelines["project-alpha"] = OnboardingOperationResult<IReadOnlyList<PipelineLookupResultDto>>.Success(
        [
            new PipelineLookupResultDto("shared-pipeline", "project-alpha", "Build Alpha", "\\CI", "/alpha.yml", "repo-alpha", "Repo Alpha")
        ]);
        lookupClient.Pipelines["project-beta"] = OnboardingOperationResult<IReadOnlyList<PipelineLookupResultDto>>.Success(
        [
            new PipelineLookupResultDto("shared-pipeline", "project-beta", "Build Beta", "\\CI", "/beta.yml", "repo-beta", "Repo Beta")
        ]);
        lookupClient.WorkItems["101"] = OnboardingOperationResult<WorkItemLookupResultDto>.Success(
            new WorkItemLookupResultDto("101", "Root Alpha", "Epic", "New", "project-alpha", "Alpha\\Area"));
        lookupClient.WorkItems["202"] = OnboardingOperationResult<WorkItemLookupResultDto>.Success(
            new WorkItemLookupResultDto("202", "Root Beta", "Epic", "New", "project-beta", "Beta\\Area"));

        var summary = await CreateService(context, lookupClient).ExecuteAsync(
            new OnboardingMigrationExecutionRequest("2026-04-06-slice-5-proof", "dev", "Manual", OnboardingMigrationExecutionMode.Live),
            CancellationToken.None);

        Assert.AreEqual(OnboardingMigrationRunStatus.PartiallySucceeded, summary.Status);
        Assert.AreEqual(0, await context.OnboardingPipelineSources.CountAsync());
        Assert.IsTrue(summary.Issues.Any(issue =>
            issue.TargetExternalIdentity == "1001"
            && issue.IsBlocking));
    }

    [TestMethod]
    public async Task ExecuteAsync_WithDuplicateWorkItemExternalIdsAcrossProjects_RecordsIssueAndDoesNotPersistInvalidRoots()
    {
        await using var connection = await CreateOpenConnectionAsync();
        await using var context = await CreateContextAsync(connection);
        await SeedDuplicateProductRootsAcrossProjectsAsync(context);

        var lookupClient = CreateLookupClient();
        lookupClient.Projects = OnboardingOperationResult<IReadOnlyList<ProjectLookupResultDto>>.Success(
        [
            new ProjectLookupResultDto("project-alpha", "Alpha", "Alpha project"),
            new ProjectLookupResultDto("project-beta", "Beta", "Beta project")
        ]);
        lookupClient.WorkItems["777"] = OnboardingOperationResult<WorkItemLookupResultDto>.Success(
            new WorkItemLookupResultDto("777", "Root Alpha", "Epic", "New", "project-alpha", "Alpha\\Area"));
        lookupClient.Pipelines["project-alpha"] = OnboardingOperationResult<IReadOnlyList<PipelineLookupResultDto>>.Success([]);
        lookupClient.Pipelines["project-beta"] = OnboardingOperationResult<IReadOnlyList<PipelineLookupResultDto>>.Success([]);

        var summary = await CreateService(context, lookupClient).ExecuteAsync(
            new OnboardingMigrationExecutionRequest("2026-04-06-slice-5-proof", "dev", "Manual", OnboardingMigrationExecutionMode.Live),
            CancellationToken.None);

        Assert.AreEqual(OnboardingMigrationRunStatus.PartiallySucceeded, summary.Status);
        Assert.AreEqual(0, await context.OnboardingProductRoots.CountAsync());
        Assert.IsTrue(summary.Issues.Any(issue =>
            issue.TargetEntityType == nameof(ProductRoot)
            && issue.IssueType == "InconsistentLegacyReference"
            && issue.IsBlocking));
    }

    [TestMethod]
    public async Task ExecuteAsync_LedgerCountsMatchBindingFailures()
    {
        await using var connection = await CreateOpenConnectionAsync();
        await using var context = await CreateContextAsync(connection);
        await SeedLegacyConfigurationAsync(context, includeBrokenTeam: true);

        var lookupClient = CreateLookupClient();
        var summary = await CreateService(context, lookupClient).ExecuteAsync(
            new OnboardingMigrationExecutionRequest("2026-04-06-slice-5-proof", "dev", "Manual", OnboardingMigrationExecutionMode.Live),
            CancellationToken.None);

        var bindingUnit = summary.Units.Single(unit => unit.UnitType == "ProductSourceBinding");
        var bindingIssues = summary.Issues.Where(issue => issue.TargetEntityType == nameof(ProductSourceBinding)).ToArray();

        Assert.AreEqual(bindingUnit.ProcessedEntityCount, bindingUnit.SucceededEntityCount + bindingUnit.FailedEntityCount + bindingUnit.SkippedEntityCount);
        Assert.HasCount(bindingUnit.FailedEntityCount, bindingIssues);
        Assert.IsTrue(bindingIssues.All(issue => issue.IsBlocking));
    }

    [TestMethod]
    public async Task ReadAsync_FingerprintIgnoresOrderingOnlyChanges()
    {
        await using var alphaConnection = await CreateOpenConnectionAsync();
        await using var betaConnection = await CreateOpenConnectionAsync();
        await using var alphaContext = await CreateContextAsync(alphaConnection);
        await using var betaContext = await CreateContextAsync(betaConnection);

        await SeedFingerprintOrderingScenarioAsync(alphaContext, reverseInsertOrder: false);
        await SeedFingerprintOrderingScenarioAsync(betaContext, reverseInsertOrder: true);

        var alphaSnapshot = await new OnboardingLegacyMigrationReader(alphaContext).ReadAsync(CancellationToken.None);
        var betaSnapshot = await new OnboardingLegacyMigrationReader(betaContext).ReadAsync(CancellationToken.None);

        Assert.AreEqual(alphaSnapshot.SourceFingerprint, betaSnapshot.SourceFingerprint);
    }

    private static OnboardingMigrationExecutionService CreateService(
        PoToolDbContext context,
        IOnboardingLiveLookupClient lookupClient,
        IOnboardingMigrationRunLock? runLock = null)
    {
        var validationService = new OnboardingValidationService(
            lookupClient,
            new OnboardingSnapshotMapper(),
            Mock.Of<IOnboardingObservability>());

        return new OnboardingMigrationExecutionService(
            context,
            new OnboardingLegacyMigrationReader(context),
            new OnboardingMigrationMapper(),
            new OnboardingMigrationLedgerService(context, new OnboardingObservability(NullLogger<OnboardingObservability>.Instance)),
            validationService,
            lookupClient,
            runLock ?? new OnboardingMigrationRunLock());
    }

    private static FakeOnboardingLiveLookupClient CreateLookupClient()
    {
        var lookupClient = new FakeOnboardingLiveLookupClient
        {
            Projects = OnboardingOperationResult<IReadOnlyList<ProjectLookupResultDto>>.Success(
            [
                new ProjectLookupResultDto("project-alpha", "Alpha", "Alpha project")
            ])
        };

        lookupClient.Teams["project-alpha"] = OnboardingOperationResult<IReadOnlyList<TeamLookupResultDto>>.Success(
        [
            new TeamLookupResultDto("team-1", "project-alpha", "Team A", null, "Alpha\\Area")
        ]);

        lookupClient.Pipelines["project-alpha"] = OnboardingOperationResult<IReadOnlyList<PipelineLookupResultDto>>.Success(
        [
            new PipelineLookupResultDto("501", "project-alpha", "Build", "\\CI", "/azure-pipelines.yml", "repo-1", "Repo A")
        ]);

        lookupClient.WorkItems["101"] = OnboardingOperationResult<WorkItemLookupResultDto>.Success(
            new WorkItemLookupResultDto("101", "Root", "Epic", "New", "project-alpha", "Alpha\\Area"));

        return lookupClient;
    }

    private static FakeOnboardingLiveLookupClient CloneLookupClient(FakeOnboardingLiveLookupClient source)
    {
        var clone = new FakeOnboardingLiveLookupClient
        {
            Projects = source.Projects
        };

        foreach (var pair in source.Teams)
        {
            clone.Teams[pair.Key] = pair.Value;
        }

        foreach (var pair in source.Pipelines)
        {
            clone.Pipelines[pair.Key] = pair.Value;
        }

        foreach (var pair in source.WorkItems)
        {
            clone.WorkItems[pair.Key] = pair.Value;
        }

        return clone;
    }

    private static async Task<string[]> CaptureOnboardingStateAsync(PoToolDbContext context)
    {
        var snapshot = new List<string>();

        snapshot.AddRange(await context.OnboardingTfsConnections
            .AsNoTracking()
            .OrderBy(item => item.ConnectionKey)
            .Select(item => $"connection|{item.ConnectionKey}|{item.OrganizationUrl}|{item.AuthenticationMode}|{item.TimeoutSeconds}|{item.ApiVersion}|{item.AvailabilityValidationState.Status}|{item.PermissionValidationState.Status}|{item.CapabilityValidationState.Status}|{item.LastVerifiedCapabilitiesSummary}")
            .ToArrayAsync());

        snapshot.AddRange(await context.OnboardingProjectSources
            .AsNoTracking()
            .OrderBy(item => item.TfsConnectionId)
            .ThenBy(item => item.ProjectExternalId)
            .Select(item => $"project|{item.TfsConnectionId}|{item.ProjectExternalId}|{item.Snapshot.Name}|{item.Snapshot.Description}|{item.ValidationState.Status}|{item.Snapshot.Metadata.ConfirmedAtUtc:O}|{item.Snapshot.Metadata.LastSeenAtUtc:O}")
            .ToArrayAsync());

        snapshot.AddRange(await context.OnboardingTeamSources
            .AsNoTracking()
            .OrderBy(item => item.ProjectSourceId)
            .ThenBy(item => item.TeamExternalId)
            .Select(item => $"team|{item.ProjectSourceId}|{item.TeamExternalId}|{item.Snapshot.Name}|{item.Snapshot.ProjectExternalId}|{item.Snapshot.DefaultAreaPath}|{item.ValidationState.Status}")
            .ToArrayAsync());

        snapshot.AddRange(await context.OnboardingPipelineSources
            .AsNoTracking()
            .OrderBy(item => item.ProjectSourceId)
            .ThenBy(item => item.PipelineExternalId)
            .Select(item => $"pipeline|{item.ProjectSourceId}|{item.PipelineExternalId}|{item.Snapshot.Name}|{item.Snapshot.ProjectExternalId}|{item.Snapshot.RepositoryExternalId}|{item.ValidationState.Status}")
            .ToArrayAsync());

        snapshot.AddRange(await context.OnboardingProductRoots
            .AsNoTracking()
            .OrderBy(item => item.ProjectSourceId)
            .ThenBy(item => item.WorkItemExternalId)
            .Select(item => $"root|{item.ProjectSourceId}|{item.WorkItemExternalId}|{item.Snapshot.Title}|{item.Snapshot.ProjectExternalId}|{item.Snapshot.AreaPath}|{item.ValidationState.Status}")
            .ToArrayAsync());

        snapshot.AddRange(await context.OnboardingProductSourceBindings
            .AsNoTracking()
            .OrderBy(item => item.ProductRootId)
            .ThenBy(item => item.SourceType)
            .ThenBy(item => item.SourceExternalId)
            .Select(item => $"binding|{item.ProductRootId}|{item.ProjectSourceId}|{item.TeamSourceId}|{item.PipelineSourceId}|{item.SourceType}|{item.SourceExternalId}|{item.ValidationState.Status}")
            .ToArrayAsync());

        return snapshot.ToArray();
    }

    private static async Task SeedLegacyConfigurationAsync(
        PoToolDbContext context,
        string connectionProjectName = "Alpha",
        string teamProjectName = "Alpha",
        bool includePipeline = true,
        bool includeRoot = true,
        bool includeBrokenTeam = false)
    {
        var localProject = new ProjectEntity
        {
            Id = "local-project",
            Alias = "local-project",
            Name = "Local Project"
        };

        var product = new ProductEntity
        {
            Name = "Product A",
            ProjectId = localProject.Id
        };

        var team = new TeamEntity
        {
            Name = "Team A",
            TeamAreaPath = "Alpha\\Area",
            ProjectName = string.IsNullOrWhiteSpace(teamProjectName) ? null : teamProjectName,
            TfsTeamId = "team-1",
            TfsTeamName = "Team A"
        };

        context.Projects.Add(localProject);
        context.Products.Add(product);
        context.Teams.Add(team);
        context.TfsConfigs.Add(new TfsConfigEntity
        {
            Url = "https://dev.azure.com/example",
            Project = connectionProjectName,
            TimeoutSeconds = 30,
            ApiVersion = "7.1",
            DefaultAreaPath = "Alpha\\Area",
            LastValidated = DateTimeOffset.UtcNow,
            HasTestedConnectionSuccessfully = true,
            HasVerifiedTfsApiSuccessfully = true,
            UpdatedAtUtc = DateTime.UtcNow
        });

        context.ProductTeamLinks.Add(new ProductTeamLinkEntity
        {
            Product = product,
            Team = team
        });

        if (includeBrokenTeam)
        {
            var brokenTeam = new TeamEntity
            {
                Name = "Broken Team",
                TeamAreaPath = "Alpha\\Broken",
                ProjectName = "Alpha",
                TfsTeamId = null,
                TfsTeamName = "Broken Team"
            };

            context.Teams.Add(brokenTeam);
            context.ProductTeamLinks.Add(new ProductTeamLinkEntity
            {
                Product = product,
                Team = brokenTeam
            });
        }

        if (includePipeline)
        {
            var repository = new RepositoryEntity
            {
                Product = product,
                Name = "Repo A"
            };

            context.Repositories.Add(repository);
            context.PipelineDefinitions.Add(new PipelineDefinitionEntity
            {
                Product = product,
                Repository = repository,
                PipelineDefinitionId = 501,
                RepoId = "repo-1",
                RepoName = "Repo A",
                Name = "Build",
                Folder = "\\CI",
                YamlPath = "/azure-pipelines.yml"
            });
        }

        if (includeRoot)
        {
            context.ProductBacklogRoots.Add(new ProductBacklogRootEntity
            {
                Product = product,
                WorkItemTfsId = 101
            });
        }

        await context.SaveChangesAsync();
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

    private static async Task<PoToolDbContext> CreateFileContextAsync(string databasePath)
    {
        var options = new DbContextOptionsBuilder<PoToolDbContext>()
            .UseSqlite($"Data Source={databasePath}")
            .Options;

        var context = new PoToolDbContext(options);
        await context.Database.EnsureCreatedAsync();
        return context;
    }

    private static async Task AssertInvalidOperationExceptionAsync(Func<Task> action)
    {
        try
        {
            await action();
            Assert.Fail("Expected InvalidOperationException.");
        }
        catch (InvalidOperationException)
        {
        }
    }

    private static async Task SeedDuplicateTeamAcrossProjectsAsync(PoToolDbContext context)
    {
        var localProject = new ProjectEntity
        {
            Id = "local-project",
            Alias = "local-project",
            Name = "Local Project"
        };

        var alphaProduct = new ProductEntity
        {
            Name = "Product Alpha",
            ProjectId = localProject.Id
        };

        var betaProduct = new ProductEntity
        {
            Name = "Product Beta",
            ProjectId = localProject.Id
        };

        var alphaTeam = new TeamEntity
        {
            Name = "Team Alpha",
            TeamAreaPath = "Alpha\\Area",
            ProjectName = "Alpha",
            TfsTeamId = "shared-team",
            TfsTeamName = "Team Alpha"
        };

        var betaTeam = new TeamEntity
        {
            Name = "Team Beta",
            TeamAreaPath = "Beta\\Area",
            ProjectName = "Beta",
            TfsTeamId = "shared-team",
            TfsTeamName = "Team Beta"
        };

        context.Projects.Add(localProject);
        context.Products.AddRange(alphaProduct, betaProduct);
        context.Teams.AddRange(alphaTeam, betaTeam);
        context.TfsConfigs.Add(new TfsConfigEntity
        {
            Url = "https://dev.azure.com/example",
            Project = "Alpha",
            TimeoutSeconds = 30,
            ApiVersion = "7.1",
            DefaultAreaPath = "Alpha\\Area",
            LastValidated = DateTimeOffset.UtcNow,
            HasTestedConnectionSuccessfully = true,
            HasVerifiedTfsApiSuccessfully = true,
            UpdatedAtUtc = DateTime.UtcNow
        });
        context.ProductTeamLinks.AddRange(
            new ProductTeamLinkEntity { Product = alphaProduct, Team = alphaTeam },
            new ProductTeamLinkEntity { Product = betaProduct, Team = betaTeam });
        context.ProductBacklogRoots.AddRange(
            new ProductBacklogRootEntity { Product = alphaProduct, WorkItemTfsId = 101 },
            new ProductBacklogRootEntity { Product = betaProduct, WorkItemTfsId = 202 });

        await context.SaveChangesAsync();
    }

    private static async Task SeedDuplicatePipelineAcrossProjectsAsync(PoToolDbContext context)
    {
        var localProject = new ProjectEntity
        {
            Id = "local-project",
            Alias = "local-project",
            Name = "Local Project"
        };

        var alphaProduct = new ProductEntity
        {
            Name = "Product Alpha",
            ProjectId = localProject.Id
        };

        var betaProduct = new ProductEntity
        {
            Name = "Product Beta",
            ProjectId = localProject.Id
        };

        context.Projects.Add(localProject);
        context.Products.AddRange(alphaProduct, betaProduct);
        context.TfsConfigs.Add(new TfsConfigEntity
        {
            Url = "https://dev.azure.com/example",
            Project = "Alpha",
            TimeoutSeconds = 30,
            ApiVersion = "7.1",
            DefaultAreaPath = "Alpha\\Area",
            LastValidated = DateTimeOffset.UtcNow,
            HasTestedConnectionSuccessfully = true,
            HasVerifiedTfsApiSuccessfully = true,
            UpdatedAtUtc = DateTime.UtcNow
        });

        var alphaRepository = new RepositoryEntity
        {
            Product = alphaProduct,
            Name = "Repo Alpha"
        };

        var betaRepository = new RepositoryEntity
        {
            Product = betaProduct,
            Name = "Repo Beta"
        };

        context.Repositories.AddRange(alphaRepository, betaRepository);
        context.PipelineDefinitions.AddRange(
            new PipelineDefinitionEntity
            {
                Product = alphaProduct,
                Repository = alphaRepository,
                PipelineDefinitionId = 1001,
                RepoId = "repo-alpha",
                RepoName = "Repo Alpha",
                Name = "Build Alpha",
                Folder = "\\CI",
                YamlPath = "/alpha.yml"
            },
            new PipelineDefinitionEntity
            {
                Product = betaProduct,
                Repository = betaRepository,
                PipelineDefinitionId = 1001,
                RepoId = "repo-beta",
                RepoName = "Repo Beta",
                Name = "Build Beta",
                Folder = "\\CI",
                YamlPath = "/beta.yml"
            });
        context.ProductBacklogRoots.AddRange(
            new ProductBacklogRootEntity { Product = alphaProduct, WorkItemTfsId = 101 },
            new ProductBacklogRootEntity { Product = betaProduct, WorkItemTfsId = 202 });

        await context.SaveChangesAsync();
    }

    private static async Task SeedDuplicateProductRootsAcrossProjectsAsync(PoToolDbContext context)
    {
        var localProject = new ProjectEntity
        {
            Id = "local-project",
            Alias = "local-project",
            Name = "Local Project"
        };

        var alphaProduct = new ProductEntity
        {
            Name = "Product Alpha",
            ProjectId = localProject.Id
        };

        var betaProduct = new ProductEntity
        {
            Name = "Product Beta",
            ProjectId = localProject.Id
        };

        context.Projects.Add(localProject);
        context.Products.AddRange(alphaProduct, betaProduct);
        context.TfsConfigs.Add(new TfsConfigEntity
        {
            Url = "https://dev.azure.com/example",
            Project = "Alpha",
            TimeoutSeconds = 30,
            ApiVersion = "7.1",
            DefaultAreaPath = "Alpha\\Area",
            LastValidated = DateTimeOffset.UtcNow,
            HasTestedConnectionSuccessfully = true,
            HasVerifiedTfsApiSuccessfully = true,
            UpdatedAtUtc = DateTime.UtcNow
        });
        context.ProductBacklogRoots.AddRange(
            new ProductBacklogRootEntity { Product = alphaProduct, WorkItemTfsId = 777 },
            new ProductBacklogRootEntity { Product = betaProduct, WorkItemTfsId = 777 });

        await context.SaveChangesAsync();
    }

    private static async Task SeedFingerprintOrderingScenarioAsync(PoToolDbContext context, bool reverseInsertOrder)
    {
        var localProject = new ProjectEntity
        {
            Id = "local-project",
            Alias = "local-project",
            Name = "Local Project"
        };

        var alphaProduct = new ProductEntity
        {
            Id = 10,
            Name = "Product Alpha",
            ProjectId = localProject.Id
        };

        var betaProduct = new ProductEntity
        {
            Id = 20,
            Name = "Product Beta",
            ProjectId = localProject.Id
        };

        var alphaTeam = new TeamEntity
        {
            Id = 100,
            Name = "Team Alpha",
            TeamAreaPath = "Alpha\\Area",
            ProjectName = "Alpha",
            TfsTeamId = "team-alpha",
            TfsTeamName = "Team Alpha"
        };

        var betaTeam = new TeamEntity
        {
            Id = 200,
            Name = "Team Beta",
            TeamAreaPath = "Beta\\Area",
            ProjectName = "Beta",
            TfsTeamId = "team-beta",
            TfsTeamName = "Team Beta"
        };

        var alphaRoot = new ProductBacklogRootEntity
        {
            ProductId = alphaProduct.Id,
            WorkItemTfsId = 101
        };

        var betaRoot = new ProductBacklogRootEntity
        {
            ProductId = betaProduct.Id,
            WorkItemTfsId = 202
        };

        context.Projects.Add(localProject);
        context.Products.AddRange(alphaProduct, betaProduct);
        context.Teams.AddRange(reverseInsertOrder ? [betaTeam, alphaTeam] : [alphaTeam, betaTeam]);
        context.TfsConfigs.Add(new TfsConfigEntity
        {
            Url = "https://dev.azure.com/example",
            Project = "Alpha",
            TimeoutSeconds = 30,
            ApiVersion = "7.1",
            DefaultAreaPath = "Alpha\\Area",
            LastValidated = new DateTimeOffset(new DateTime(2026, 4, 6, 9, 0, 0, DateTimeKind.Utc)),
            HasTestedConnectionSuccessfully = true,
            HasVerifiedTfsApiSuccessfully = true,
            UpdatedAtUtc = new DateTime(2026, 4, 6, 9, 0, 0, DateTimeKind.Utc)
        });

        var alphaLink = new ProductTeamLinkEntity { ProductId = alphaProduct.Id, TeamId = alphaTeam.Id };
        var betaLink = new ProductTeamLinkEntity { ProductId = betaProduct.Id, TeamId = betaTeam.Id };
        context.ProductTeamLinks.AddRange(reverseInsertOrder ? [betaLink, alphaLink] : [alphaLink, betaLink]);
        context.ProductBacklogRoots.AddRange(reverseInsertOrder ? [betaRoot, alphaRoot] : [alphaRoot, betaRoot]);

        await context.SaveChangesAsync();
    }

    private class FakeOnboardingLiveLookupClient : IOnboardingLiveLookupClient
    {
        public OnboardingOperationResult<IReadOnlyList<ProjectLookupResultDto>> Projects { get; set; }
            = OnboardingOperationResult<IReadOnlyList<ProjectLookupResultDto>>.Success([]);

        public Dictionary<string, OnboardingOperationResult<IReadOnlyList<TeamLookupResultDto>>> Teams { get; } = new(StringComparer.OrdinalIgnoreCase);

        public Dictionary<string, OnboardingOperationResult<IReadOnlyList<PipelineLookupResultDto>>> Pipelines { get; } = new(StringComparer.OrdinalIgnoreCase);

        public Dictionary<string, OnboardingOperationResult<WorkItemLookupResultDto>> WorkItems { get; } = new(StringComparer.OrdinalIgnoreCase);

        public Task<OnboardingOperationResult<IReadOnlyList<ProjectLookupResultDto>>> GetProjectsAsync(
            TfsConnection connection,
            string? query,
            int top,
            int skip,
            CancellationToken cancellationToken)
            => Task.FromResult(Projects);

        public Task<OnboardingOperationResult<IReadOnlyList<TeamLookupResultDto>>> GetTeamsAsync(
            TfsConnection connection,
            string projectExternalId,
            string? query,
            int top,
            int skip,
            CancellationToken cancellationToken)
            => Task.FromResult(
                Teams.TryGetValue(projectExternalId, out var result)
                    ? result
                    : OnboardingOperationResult<IReadOnlyList<TeamLookupResultDto>>.Success([]));

        public Task<OnboardingOperationResult<IReadOnlyList<PipelineLookupResultDto>>> GetPipelinesAsync(
            TfsConnection connection,
            string projectExternalId,
            string? query,
            int top,
            int skip,
            CancellationToken cancellationToken)
            => Task.FromResult(
                Pipelines.TryGetValue(projectExternalId, out var result)
                    ? result
                    : OnboardingOperationResult<IReadOnlyList<PipelineLookupResultDto>>.Success([]));

        public Task<OnboardingOperationResult<IReadOnlyList<WorkItemLookupResultDto>>> SearchWorkItemsAsync(
            TfsConnection connection,
            string? query,
            string? projectExternalId,
            IReadOnlyCollection<string>? workItemTypes,
            int top,
            int skip,
            CancellationToken cancellationToken)
            => Task.FromResult(OnboardingOperationResult<IReadOnlyList<WorkItemLookupResultDto>>.Success([]));

        public Task<OnboardingOperationResult<WorkItemLookupResultDto>> GetWorkItemAsync(
            TfsConnection connection,
            string workItemExternalId,
            CancellationToken cancellationToken)
            => Task.FromResult(
                WorkItems.TryGetValue(workItemExternalId, out var result)
                    ? result
                    : OnboardingOperationResult<WorkItemLookupResultDto>.Failure(
                        new OnboardingErrorDto(OnboardingErrorCode.NotFound, "Missing", workItemExternalId, false)));
    }

    private sealed class BlockingOnboardingLiveLookupClient : IOnboardingLiveLookupClient
    {
        private readonly FakeOnboardingLiveLookupClient _inner;
        private readonly TaskCompletionSource _blocked = new(TaskCreationOptions.RunContinuationsAsynchronously);
        private readonly TaskCompletionSource _release = new(TaskCreationOptions.RunContinuationsAsynchronously);

        public BlockingOnboardingLiveLookupClient(FakeOnboardingLiveLookupClient inner)
        {
            _inner = inner;
        }

        public Task WaitUntilBlockedAsync() => _blocked.Task;

        public void Release() => _release.TrySetResult();

        public async Task<OnboardingOperationResult<IReadOnlyList<ProjectLookupResultDto>>> GetProjectsAsync(
            TfsConnection connection,
            string? query,
            int top,
            int skip,
            CancellationToken cancellationToken)
        {
            _blocked.TrySetResult();
            await _release.Task.WaitAsync(cancellationToken);
            return await _inner.GetProjectsAsync(connection, query, top, skip, cancellationToken);
        }

        public Task<OnboardingOperationResult<IReadOnlyList<TeamLookupResultDto>>> GetTeamsAsync(
            TfsConnection connection,
            string projectExternalId,
            string? query,
            int top,
            int skip,
            CancellationToken cancellationToken)
            => _inner.GetTeamsAsync(connection, projectExternalId, query, top, skip, cancellationToken);

        public Task<OnboardingOperationResult<IReadOnlyList<PipelineLookupResultDto>>> GetPipelinesAsync(
            TfsConnection connection,
            string projectExternalId,
            string? query,
            int top,
            int skip,
            CancellationToken cancellationToken)
            => _inner.GetPipelinesAsync(connection, projectExternalId, query, top, skip, cancellationToken);

        public Task<OnboardingOperationResult<IReadOnlyList<WorkItemLookupResultDto>>> SearchWorkItemsAsync(
            TfsConnection connection,
            string? query,
            string? projectExternalId,
            IReadOnlyCollection<string>? workItemTypes,
            int top,
            int skip,
            CancellationToken cancellationToken)
            => _inner.SearchWorkItemsAsync(connection, query, projectExternalId, workItemTypes, top, skip, cancellationToken);

        public Task<OnboardingOperationResult<WorkItemLookupResultDto>> GetWorkItemAsync(
            TfsConnection connection,
            string workItemExternalId,
            CancellationToken cancellationToken)
            => _inner.GetWorkItemAsync(connection, workItemExternalId, cancellationToken);
    }
}
