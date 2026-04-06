using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using PoTool.Api.Handlers.Onboarding;
using PoTool.Api.Persistence;
using PoTool.Api.Persistence.Entities.Onboarding;
using PoTool.Api.Services.Onboarding;

namespace PoTool.Tests.Unit.Services.Onboarding;

[TestClass]
public sealed class OnboardingMigrationLedgerServiceTests
{
    [TestMethod]
    public async Task CreateMigrationRun_PersistsInitialInfrastructureState()
    {
        await using var connection = await CreateOpenConnectionAsync();
        await using var context = await CreateContextAsync(connection);
        var service = CreateLedgerService(context);

        var run = await service.CreateRunAsync(
            new OnboardingMigrationRunCreateRequest(
                "2026-04-06-slice-4",
                "dev",
                "Manual",
                OnboardingMigrationExecutionMode.DryRun,
                "fingerprint-a"),
            CancellationToken.None);

        Assert.AreEqual(OnboardingMigrationRunStatus.NotStarted, run.Status);
        Assert.AreEqual(OnboardingMigrationExecutionMode.DryRun, run.ExecutionMode);
        Assert.AreEqual("2026-04-06-slice-4", run.MigrationVersion);
        Assert.AreEqual(1, await context.OnboardingMigrationRuns.CountAsync());
    }

    [TestMethod]
    public async Task CreateOrderedUnits_PersistsPendingUnitsInOrder()
    {
        await using var connection = await CreateOpenConnectionAsync();
        await using var context = await CreateContextAsync(connection);
        var service = CreateLedgerService(context);
        var run = await CreateRunAsync(service);

        await service.CreateUnitsAsync(
            run.RunIdentifier,
            [
                new OnboardingMigrationUnitPlan("PipelineSource", "pipelines", 4),
                new OnboardingMigrationUnitPlan("Connection", "connection", 1),
                new OnboardingMigrationUnitPlan("ProjectSource", "projects", 2)
            ],
            CancellationToken.None);

        var persistedUnits = await context.OnboardingMigrationUnits
            .OrderBy(unit => unit.ExecutionOrder)
            .ToListAsync();

        CollectionAssert.AreEqual(new[] { 1, 2, 4 }, persistedUnits.Select(unit => unit.ExecutionOrder).ToArray());
        Assert.IsTrue(persistedUnits.All(unit => unit.Status == OnboardingMigrationUnitStatus.Pending));
    }

    [TestMethod]
    public async Task MarkUnitRunningSucceededFailedAndSkipped_UpdatesTerminalCounts()
    {
        await using var connection = await CreateOpenConnectionAsync();
        await using var context = await CreateContextAsync(connection);
        var service = CreateLedgerService(context);
        var run = await CreateRunAsync(service);

        var units = await service.CreateUnitsAsync(
            run.RunIdentifier,
            [
                new OnboardingMigrationUnitPlan("Connection", "connection", 1),
                new OnboardingMigrationUnitPlan("ProjectSource", "projects", 2),
                new OnboardingMigrationUnitPlan("TeamSource", "teams", 3)
            ],
            CancellationToken.None);

        await service.StartUnitAsync(units[0].UnitIdentifier, CancellationToken.None);
        await service.CompleteUnitAsync(units[0].UnitIdentifier, new OnboardingMigrationUnitOutcome(5, 5, 0, 0), CancellationToken.None);
        await service.FailUnitAsync(units[1].UnitIdentifier, new OnboardingMigrationUnitOutcome(3, 1, 2, 0), CancellationToken.None);
        await service.SkipUnitAsync(units[2].UnitIdentifier, new OnboardingMigrationUnitOutcome(0, 0, 0, 4), CancellationToken.None);

        var persistedUnits = await context.OnboardingMigrationUnits
            .OrderBy(unit => unit.ExecutionOrder)
            .ToListAsync();

        Assert.AreEqual(OnboardingMigrationUnitStatus.Succeeded, persistedUnits[0].Status);
        Assert.AreEqual(OnboardingMigrationUnitStatus.Failed, persistedUnits[1].Status);
        Assert.AreEqual(OnboardingMigrationUnitStatus.Skipped, persistedUnits[2].Status);
        Assert.IsTrue(persistedUnits.All(unit => unit.StartedAtUtc.HasValue));
        Assert.IsTrue(persistedUnits.All(unit => unit.FinishedAtUtc.HasValue));
    }

    [TestMethod]
    public async Task RecordBlockingAndNonBlockingIssues_PersistsSeverityAndBlockingFlags()
    {
        await using var connection = await CreateOpenConnectionAsync();
        await using var context = await CreateContextAsync(connection);
        var service = CreateLedgerService(context);
        var run = await CreateRunAsync(service);
        var unit = (await service.CreateUnitsAsync(
            run.RunIdentifier,
            [new OnboardingMigrationUnitPlan("ProjectSource", "projects", 1)],
            CancellationToken.None)).Single();

        await service.RecordIssueAsync(
            run.RunIdentifier,
            new OnboardingMigrationIssueCreateRequest(
                unit.UnitIdentifier,
                "MissingIdentity",
                "Resolution",
                OnboardingMigrationIssueSeverity.Blocking,
                "legacy:project:42",
                "ProjectSource",
                null,
                "The project is missing an external identity.",
                null,
                IsBlocking: false),
            CancellationToken.None);

        await service.RecordIssueAsync(
            run.RunIdentifier,
            new OnboardingMigrationIssueCreateRequest(
                null,
                "SoftWarning",
                "Snapshot",
                OnboardingMigrationIssueSeverity.Warning,
                "legacy:project:99",
                "ProjectSource",
                "project-99",
                "The snapshot metadata is stale.",
                null,
                IsBlocking: false),
            CancellationToken.None);

        var issues = await context.OnboardingMigrationIssues
            .OrderBy(issue => issue.CreatedAtUtc)
            .ToListAsync();

        Assert.HasCount(2, issues);
        Assert.IsTrue(issues[0].IsBlocking);
        Assert.IsFalse(issues[1].IsBlocking);
        Assert.AreEqual(OnboardingMigrationIssueSeverity.Warning, issues[1].Severity);
    }

    [TestMethod]
    public async Task FinalizeRun_AllSucceededUnits_SetsSucceededStatus()
    {
        await using var connection = await CreateOpenConnectionAsync();
        await using var context = await CreateContextAsync(connection);
        var service = CreateLedgerService(context);
        var run = await CreateRunAsync(service);
        var units = await service.CreateUnitsAsync(
            run.RunIdentifier,
            [
                new OnboardingMigrationUnitPlan("Connection", "connection", 1),
                new OnboardingMigrationUnitPlan("ProjectSource", "projects", 2)
            ],
            CancellationToken.None);

        foreach (var unit in units)
        {
            await service.CompleteUnitAsync(unit.UnitIdentifier, new OnboardingMigrationUnitOutcome(2, 2, 0, 0), CancellationToken.None);
        }

        var finalized = await service.FinalizeRunAsync(run.RunIdentifier, CancellationToken.None);

        Assert.AreEqual(OnboardingMigrationRunStatus.Succeeded, finalized.Status);
        Assert.AreEqual(2, finalized.SucceededUnitCount);
        Assert.AreEqual(4, finalized.ProcessedEntityCount);
    }

    [TestMethod]
    public async Task FinalizeRun_MixedSucceededAndFailedUnits_SetsPartiallySucceededStatus()
    {
        await using var connection = await CreateOpenConnectionAsync();
        await using var context = await CreateContextAsync(connection);
        var service = CreateLedgerService(context);
        var run = await CreateRunAsync(service);
        var units = await service.CreateUnitsAsync(
            run.RunIdentifier,
            [
                new OnboardingMigrationUnitPlan("Connection", "connection", 1),
                new OnboardingMigrationUnitPlan("ProjectSource", "projects", 2)
            ],
            CancellationToken.None);

        await service.CompleteUnitAsync(units[0].UnitIdentifier, new OnboardingMigrationUnitOutcome(2, 2, 0, 0), CancellationToken.None);
        await service.FailUnitAsync(units[1].UnitIdentifier, new OnboardingMigrationUnitOutcome(3, 1, 2, 0), CancellationToken.None);

        var finalized = await service.FinalizeRunAsync(run.RunIdentifier, CancellationToken.None);

        Assert.AreEqual(OnboardingMigrationRunStatus.PartiallySucceeded, finalized.Status);
        Assert.AreEqual(1, finalized.SucceededUnitCount);
        Assert.AreEqual(1, finalized.FailedUnitCount);
    }

    [TestMethod]
    public async Task CancelledRun_RemainsCancelledWhenFinalized()
    {
        await using var connection = await CreateOpenConnectionAsync();
        await using var context = await CreateContextAsync(connection);
        var service = CreateLedgerService(context);
        var run = await CreateRunAsync(service);
        var units = await service.CreateUnitsAsync(
            run.RunIdentifier,
            [new OnboardingMigrationUnitPlan("Connection", "connection", 1)],
            CancellationToken.None);

        await service.CompleteUnitAsync(units[0].UnitIdentifier, new OnboardingMigrationUnitOutcome(1, 1, 0, 0), CancellationToken.None);
        await service.CancelRunAsync(run.RunIdentifier, CancellationToken.None);

        var finalized = await service.FinalizeRunAsync(run.RunIdentifier, CancellationToken.None);

        Assert.AreEqual(OnboardingMigrationRunStatus.Cancelled, finalized.Status);
    }

    [TestMethod]
    public async Task DryRun_CreatesRunUnitsAndIssues_WithoutWritingOnboardingBusinessEntities()
    {
        await using var connection = await CreateOpenConnectionAsync();
        await using var context = await CreateContextAsync(connection);
        var service = CreateLedgerService(context);
        var handler = new OnboardingMigrationJobHandler(service);

        var summary = await handler.RunDryRunAsync(
            new OnboardingMigrationDryRunRequest(
                "2026-04-06-slice-4",
                "dev",
                "Manual",
                "fingerprint-dry-run",
                [
                    new OnboardingMigrationDryRunUnitPlan(
                        "Connection",
                        "connection",
                        1,
                        OnboardingMigrationUnitStatus.Succeeded,
                        new OnboardingMigrationUnitOutcome(1, 1, 0, 0),
                        []),
                    new OnboardingMigrationDryRunUnitPlan(
                        "ProjectSource",
                        "projects",
                        2,
                        OnboardingMigrationUnitStatus.Failed,
                        new OnboardingMigrationUnitOutcome(2, 1, 1, 0),
                        [
                            new OnboardingMigrationDryRunIssuePlan(
                                "MissingIdentity",
                                "Resolution",
                                OnboardingMigrationIssueSeverity.Blocking,
                                "legacy:project:7",
                                "ProjectSource",
                                null,
                                "The project is missing an external identity.",
                                "The legacy row did not contain a stable external identifier.",
                                true)
                        ])
                ]),
            CancellationToken.None);

        Assert.AreEqual(OnboardingMigrationExecutionMode.DryRun, summary.ExecutionMode);
        Assert.AreEqual(2, summary.TotalUnitCount);
        Assert.AreEqual(1, summary.IssueCount);
        Assert.AreEqual(OnboardingMigrationRunStatus.PartiallySucceeded, summary.Status);
        Assert.AreEqual(0, await context.OnboardingTfsConnections.CountAsync());
        Assert.AreEqual(0, await context.OnboardingProjectSources.CountAsync());
        Assert.AreEqual(0, await context.OnboardingProductRoots.CountAsync());
        Assert.AreEqual(1, await context.OnboardingMigrationRuns.CountAsync());
        Assert.AreEqual(2, await context.OnboardingMigrationUnits.CountAsync());
        Assert.AreEqual(1, await context.OnboardingMigrationIssues.CountAsync());
    }

    private static OnboardingMigrationLedgerService CreateLedgerService(PoToolDbContext context)
        => new(context, new OnboardingObservability(NullLogger<OnboardingObservability>.Instance));

    private static async Task<MigrationRun> CreateRunAsync(IOnboardingMigrationLedgerService service)
        => await service.CreateRunAsync(
            new OnboardingMigrationRunCreateRequest(
                "2026-04-06-slice-4",
                "dev",
                "Manual",
                OnboardingMigrationExecutionMode.DryRun,
                "fingerprint-a"),
            CancellationToken.None);

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
}
