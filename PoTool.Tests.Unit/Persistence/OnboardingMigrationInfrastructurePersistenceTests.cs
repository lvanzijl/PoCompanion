using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using PoTool.Api.Persistence;
using PoTool.Api.Persistence.Entities.Onboarding;

namespace PoTool.Tests.Unit.Persistence;

[TestClass]
public sealed class OnboardingMigrationInfrastructurePersistenceTests
{
    [TestMethod]
    public async Task InsertValidMigrationGraph_Succeeds()
    {
        await using var connection = await CreateOpenConnectionAsync();
        await using var context = await CreateContextAsync(connection);

        var utcNow = new DateTime(2026, 4, 6, 8, 0, 0, DateTimeKind.Utc);
        var run = CreateRun(utcNow);
        var unit = CreateUnit(utcNow);
        var issue = CreateIssue(utcNow);
        issue.MigrationRun = run;

        run.Units.Add(unit);
        run.Issues.Add(issue);
        unit.Issues.Add(CreateUnitIssue(run, unit, utcNow.AddMinutes(1)));

        context.OnboardingMigrationRuns.Add(run);

        await context.SaveChangesAsync();

        Assert.AreEqual(1, await context.OnboardingMigrationRuns.CountAsync());
        Assert.AreEqual(1, await context.OnboardingMigrationUnits.CountAsync());
        Assert.AreEqual(2, await context.OnboardingMigrationIssues.CountAsync());
    }

    [TestMethod]
    public async Task MissingRequiredRunField_IsRejected()
    {
        await using var connection = await CreateOpenConnectionAsync();
        await using var context = await CreateContextAsync(connection);

        var run = CreateRun(new DateTime(2026, 4, 6, 8, 10, 0, DateTimeKind.Utc));
        run.MigrationVersion = null!;

        context.OnboardingMigrationRuns.Add(run);

        await Assert.ThrowsExactlyAsync<DbUpdateException>(() => context.SaveChangesAsync());
    }

    [TestMethod]
    public async Task MissingRequiredParentReference_IsRejected()
    {
        await using var connection = await CreateOpenConnectionAsync();
        await using var context = await CreateContextAsync(connection);

        context.OnboardingMigrationUnits.Add(new MigrationUnit
        {
            MigrationRunId = 999,
            UnitType = "ProjectSource",
            UnitName = "project sources",
            ExecutionOrder = 2,
            Status = OnboardingMigrationUnitStatus.Pending,
            CreatedAtUtc = DateTime.UtcNow,
            UpdatedAtUtc = DateTime.UtcNow
        });

        await Assert.ThrowsExactlyAsync<InvalidOperationException>(() => context.SaveChangesAsync());
    }

    [TestMethod]
    public async Task InvalidOptionalUnitReference_IsRejected()
    {
        await using var connection = await CreateOpenConnectionAsync();
        await using var context = await CreateContextAsync(connection);

        var run = CreateRun(new DateTime(2026, 4, 6, 8, 20, 0, DateTimeKind.Utc));
        context.OnboardingMigrationRuns.Add(run);
        await context.SaveChangesAsync();

        context.OnboardingMigrationIssues.Add(new MigrationIssue
        {
            MigrationRunId = run.Id,
            MigrationUnitId = 999,
            IssueType = "MissingUnit",
            IssueCategory = "Relationship",
            Severity = OnboardingMigrationIssueSeverity.Warning,
            SourceLegacyReference = "legacy:unit:999",
            TargetEntityType = "MigrationUnit",
            SanitizedMessage = "The referenced migration unit does not exist.",
            SanitizedDetails = null,
            IsBlocking = false,
            CreatedAtUtc = DateTime.UtcNow,
            UpdatedAtUtc = DateTime.UtcNow
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

    private static MigrationRun CreateRun(DateTime utcNow)
        => new()
        {
            RunIdentifier = Guid.Parse("10000000-0000-0000-0000-000000000001"),
            MigrationVersion = "2026-04-06-slice-4",
            EnvironmentRing = "dev",
            TriggerType = "Manual",
            ExecutionMode = OnboardingMigrationExecutionMode.DryRun,
            SourceFingerprint = "fingerprint-1",
            StartedAtUtc = utcNow,
            FinishedAtUtc = utcNow.AddMinutes(5),
            Status = OnboardingMigrationRunStatus.Succeeded,
            TotalUnitCount = 1,
            SucceededUnitCount = 1,
            ProcessedEntityCount = 10,
            SucceededEntityCount = 10,
            IssueCount = 1,
            BlockingIssueCount = 1,
            CreatedAtUtc = utcNow,
            UpdatedAtUtc = utcNow
        };

    private static MigrationUnit CreateUnit(DateTime utcNow)
        => new()
        {
            UnitIdentifier = Guid.Parse("20000000-0000-0000-0000-000000000001"),
            UnitType = "ProjectSource",
            UnitName = "project sources",
            ExecutionOrder = 2,
            StartedAtUtc = utcNow,
            FinishedAtUtc = utcNow.AddMinutes(1),
            Status = OnboardingMigrationUnitStatus.Succeeded,
            ProcessedEntityCount = 5,
            SucceededEntityCount = 5,
            CreatedAtUtc = utcNow,
            UpdatedAtUtc = utcNow
        };

    private static MigrationIssue CreateIssue(DateTime utcNow)
        => new()
        {
            IssueIdentifier = Guid.NewGuid(),
            IssueType = "MissingExternalId",
            IssueCategory = "Resolution",
            Severity = OnboardingMigrationIssueSeverity.Blocking,
            SourceLegacyReference = "legacy:project:42",
            TargetEntityType = "ProjectSource",
            TargetExternalIdentity = null,
            SanitizedMessage = "The legacy record is missing an external identity.",
            SanitizedDetails = "No stable external identifier was available for migration.",
            IsBlocking = true,
            CreatedAtUtc = utcNow,
            UpdatedAtUtc = utcNow
        };

    private static MigrationIssue CreateUnitIssue(MigrationRun run, MigrationUnit unit, DateTime utcNow)
    {
        var issue = CreateIssue(utcNow);
        issue.MigrationRun = run;
        issue.MigrationUnit = unit;
        return issue;
    }
}
