using Microsoft.EntityFrameworkCore;
using PoTool.Api.Persistence;
using PoTool.Api.Persistence.Entities.Onboarding;

namespace PoTool.Api.Services.Onboarding;

public interface IOnboardingMigrationLedgerService
{
    Task<MigrationRun> CreateRunAsync(OnboardingMigrationRunCreateRequest request, CancellationToken cancellationToken);
    Task<IReadOnlyList<MigrationUnit>> CreateUnitsAsync(Guid runIdentifier, IReadOnlyList<OnboardingMigrationUnitPlan> units, CancellationToken cancellationToken);
    Task<MigrationUnit> StartUnitAsync(Guid unitIdentifier, CancellationToken cancellationToken);
    Task<MigrationUnit> CompleteUnitAsync(Guid unitIdentifier, OnboardingMigrationUnitOutcome outcome, CancellationToken cancellationToken);
    Task<MigrationUnit> FailUnitAsync(Guid unitIdentifier, OnboardingMigrationUnitOutcome outcome, CancellationToken cancellationToken);
    Task<MigrationUnit> SkipUnitAsync(Guid unitIdentifier, OnboardingMigrationUnitOutcome outcome, CancellationToken cancellationToken);
    Task<MigrationIssue> RecordIssueAsync(Guid runIdentifier, OnboardingMigrationIssueCreateRequest request, CancellationToken cancellationToken);
    Task<MigrationRun> CancelRunAsync(Guid runIdentifier, CancellationToken cancellationToken);
    Task<MigrationRun> FinalizeRunAsync(Guid runIdentifier, CancellationToken cancellationToken);
    Task<OnboardingMigrationRunSummary> GetRunSummaryAsync(Guid runIdentifier, CancellationToken cancellationToken);
}

public sealed class OnboardingMigrationLedgerService : IOnboardingMigrationLedgerService
{
    private readonly PoToolDbContext _dbContext;
    private readonly IOnboardingObservability _observability;

    public OnboardingMigrationLedgerService(PoToolDbContext dbContext, IOnboardingObservability observability)
    {
        _dbContext = dbContext;
        _observability = observability;
    }

    public async Task<MigrationRun> CreateRunAsync(
        OnboardingMigrationRunCreateRequest request,
        CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(request.MigrationVersion);
        ArgumentException.ThrowIfNullOrWhiteSpace(request.EnvironmentRing);
        ArgumentException.ThrowIfNullOrWhiteSpace(request.TriggerType);

        var run = new MigrationRun
        {
            MigrationVersion = request.MigrationVersion.Trim(),
            EnvironmentRing = request.EnvironmentRing.Trim(),
            TriggerType = request.TriggerType.Trim(),
            ExecutionMode = request.ExecutionMode,
            SourceFingerprint = string.IsNullOrWhiteSpace(request.SourceFingerprint)
                ? null
                : request.SourceFingerprint.Trim(),
            Status = OnboardingMigrationRunStatus.NotStarted
        };

        _dbContext.OnboardingMigrationRuns.Add(run);
        await _dbContext.SaveChangesAsync(cancellationToken);

        _observability.RecordMigrationRunStatus(run.Status.ToString(), run.ExecutionMode.ToString());

        return run;
    }

    public async Task<IReadOnlyList<MigrationUnit>> CreateUnitsAsync(
        Guid runIdentifier,
        IReadOnlyList<OnboardingMigrationUnitPlan> units,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(units);

        var run = await GetRunAsync(runIdentifier, cancellationToken);
        EnsureRunIsMutable(run);

        if (units.Count == 0)
        {
            run.TotalUnitCount = 0;
            run.UpdatedAtUtc = DateTime.UtcNow;
            await _dbContext.SaveChangesAsync(cancellationToken);
            return Array.Empty<MigrationUnit>();
        }

        var duplicateOrder = units
            .GroupBy(unit => unit.ExecutionOrder)
            .FirstOrDefault(group => group.Count() > 1);

        if (duplicateOrder is not null)
        {
            throw new InvalidOperationException($"Migration unit execution order '{duplicateOrder.Key}' must be unique within a run.");
        }

        var createdUnits = units
            .OrderBy(unit => unit.ExecutionOrder)
            .Select(unit =>
            {
                ArgumentException.ThrowIfNullOrWhiteSpace(unit.UnitType);
                ArgumentException.ThrowIfNullOrWhiteSpace(unit.UnitName);

                return new MigrationUnit
                {
                    MigrationRunId = run.Id,
                    UnitType = unit.UnitType.Trim(),
                    UnitName = unit.UnitName.Trim(),
                    ExecutionOrder = unit.ExecutionOrder,
                    Status = OnboardingMigrationUnitStatus.Pending
                };
            })
            .ToArray();

        _dbContext.OnboardingMigrationUnits.AddRange(createdUnits);
        run.TotalUnitCount = createdUnits.Length;
        run.UpdatedAtUtc = DateTime.UtcNow;

        await _dbContext.SaveChangesAsync(cancellationToken);

        foreach (var unit in createdUnits)
        {
            _observability.RecordMigrationUnitStatus(unit.Status.ToString(), unit.UnitType);
        }

        return createdUnits;
    }

    public Task<MigrationUnit> StartUnitAsync(Guid unitIdentifier, CancellationToken cancellationToken)
        => TransitionUnitAsync(unitIdentifier, OnboardingMigrationUnitStatus.Running, outcome: null, cancellationToken);

    public Task<MigrationUnit> CompleteUnitAsync(
        Guid unitIdentifier,
        OnboardingMigrationUnitOutcome outcome,
        CancellationToken cancellationToken)
        => TransitionUnitAsync(unitIdentifier, OnboardingMigrationUnitStatus.Succeeded, outcome, cancellationToken);

    public Task<MigrationUnit> FailUnitAsync(
        Guid unitIdentifier,
        OnboardingMigrationUnitOutcome outcome,
        CancellationToken cancellationToken)
        => TransitionUnitAsync(unitIdentifier, OnboardingMigrationUnitStatus.Failed, outcome, cancellationToken);

    public Task<MigrationUnit> SkipUnitAsync(
        Guid unitIdentifier,
        OnboardingMigrationUnitOutcome outcome,
        CancellationToken cancellationToken)
        => TransitionUnitAsync(unitIdentifier, OnboardingMigrationUnitStatus.Skipped, outcome, cancellationToken);

    public async Task<MigrationIssue> RecordIssueAsync(
        Guid runIdentifier,
        OnboardingMigrationIssueCreateRequest request,
        CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(request.IssueType);
        ArgumentException.ThrowIfNullOrWhiteSpace(request.IssueCategory);
        ArgumentException.ThrowIfNullOrWhiteSpace(request.SourceLegacyReference);
        ArgumentException.ThrowIfNullOrWhiteSpace(request.TargetEntityType);
        ArgumentException.ThrowIfNullOrWhiteSpace(request.SanitizedMessage);

        var run = await _dbContext.OnboardingMigrationRuns
            .SingleAsync(entity => entity.RunIdentifier == runIdentifier, cancellationToken);

        MigrationUnit? unit = null;
        if (request.UnitIdentifier.HasValue)
        {
            unit = await _dbContext.OnboardingMigrationUnits
                .SingleAsync(
                    entity => entity.UnitIdentifier == request.UnitIdentifier.Value,
                    cancellationToken);

            if (unit.MigrationRunId != run.Id)
            {
                throw new InvalidOperationException("Migration issue unit must belong to the same migration run.");
            }
        }

        var issue = new MigrationIssue
        {
            MigrationRunId = run.Id,
            MigrationUnitId = unit?.Id,
            IssueType = request.IssueType.Trim(),
            IssueCategory = request.IssueCategory.Trim(),
            Severity = request.Severity,
            SourceLegacyReference = request.SourceLegacyReference.Trim(),
            TargetEntityType = request.TargetEntityType.Trim(),
            TargetExternalIdentity = string.IsNullOrWhiteSpace(request.TargetExternalIdentity)
                ? null
                : request.TargetExternalIdentity.Trim(),
            SanitizedMessage = request.SanitizedMessage.Trim(),
            SanitizedDetails = string.IsNullOrWhiteSpace(request.SanitizedDetails)
                ? null
                : request.SanitizedDetails.Trim(),
            IsBlocking = request.IsBlocking || request.Severity == OnboardingMigrationIssueSeverity.Blocking
        };

        _dbContext.OnboardingMigrationIssues.Add(issue);
        run.UpdatedAtUtc = DateTime.UtcNow;
        await _dbContext.SaveChangesAsync(cancellationToken);

        _observability.LogMigrationIssueRecorded(
            run.RunIdentifier,
            unit?.UnitIdentifier,
            issue.IssueCategory,
            issue.Severity.ToString(),
            issue.IsBlocking);
        _observability.RecordMigrationIssue(issue.Severity.ToString(), issue.IssueCategory, issue.IsBlocking);

        return issue;
    }

    public async Task<MigrationRun> CancelRunAsync(Guid runIdentifier, CancellationToken cancellationToken)
    {
        var run = await _dbContext.OnboardingMigrationRuns
            .Include(entity => entity.Units)
            .Include(entity => entity.Issues)
            .SingleAsync(entity => entity.RunIdentifier == runIdentifier, cancellationToken);

        run.Status = OnboardingMigrationRunStatus.Cancelled;
        run.FinishedAtUtc ??= DateTime.UtcNow;
        RefreshRunSummary(run);

        await _dbContext.SaveChangesAsync(cancellationToken);

        LogRunFinalized(run);

        return run;
    }

    public async Task<MigrationRun> FinalizeRunAsync(Guid runIdentifier, CancellationToken cancellationToken)
    {
        var run = await _dbContext.OnboardingMigrationRuns
            .Include(entity => entity.Units)
            .Include(entity => entity.Issues)
            .SingleAsync(entity => entity.RunIdentifier == runIdentifier, cancellationToken);

        RefreshRunSummary(run);

        if (run.Status != OnboardingMigrationRunStatus.Cancelled)
        {
            if (run.Units.Any(unit => unit.Status is OnboardingMigrationUnitStatus.Pending or OnboardingMigrationUnitStatus.Running))
            {
                throw new InvalidOperationException("Migration run cannot be finalized while units are still pending or running.");
            }

            run.Status = DetermineRunStatus(run.Units);
        }

        run.FinishedAtUtc ??= DateTime.UtcNow;
        run.UpdatedAtUtc = DateTime.UtcNow;

        await _dbContext.SaveChangesAsync(cancellationToken);

        LogRunFinalized(run);

        return run;
    }

    public async Task<OnboardingMigrationRunSummary> GetRunSummaryAsync(Guid runIdentifier, CancellationToken cancellationToken)
    {
        var run = await _dbContext.OnboardingMigrationRuns
            .AsNoTracking()
            .Include(entity => entity.Units)
            .Include(entity => entity.Issues)
            .SingleAsync(entity => entity.RunIdentifier == runIdentifier, cancellationToken);

        return MapSummary(run);
    }

    private async Task<MigrationUnit> TransitionUnitAsync(
        Guid unitIdentifier,
        OnboardingMigrationUnitStatus targetStatus,
        OnboardingMigrationUnitOutcome? outcome,
        CancellationToken cancellationToken)
    {
        var unit = await _dbContext.OnboardingMigrationUnits
            .Include(entity => entity.MigrationRun)
            .SingleAsync(entity => entity.UnitIdentifier == unitIdentifier, cancellationToken);

        EnsureRunIsMutable(unit.MigrationRun);

        if (targetStatus == OnboardingMigrationUnitStatus.Running)
        {
            if (unit.Status != OnboardingMigrationUnitStatus.Pending)
            {
                throw new InvalidOperationException("Only pending migration units can be started.");
            }

            unit.StartedAtUtc ??= DateTime.UtcNow;
            unit.Status = OnboardingMigrationUnitStatus.Running;

            if (!unit.MigrationRun.StartedAtUtc.HasValue)
            {
                unit.MigrationRun.StartedAtUtc = unit.StartedAtUtc;
                unit.MigrationRun.Status = OnboardingMigrationRunStatus.Running;
                _observability.LogMigrationRunStarted(
                    unit.MigrationRun.RunIdentifier,
                    unit.MigrationRun.MigrationVersion,
                    unit.MigrationRun.EnvironmentRing,
                    unit.MigrationRun.ExecutionMode.ToString(),
                    unit.MigrationRun.TriggerType);
                _observability.RecordMigrationRunStatus(
                    unit.MigrationRun.Status.ToString(),
                    unit.MigrationRun.ExecutionMode.ToString());
            }

            unit.UpdatedAtUtc = DateTime.UtcNow;
            unit.MigrationRun.UpdatedAtUtc = DateTime.UtcNow;
            await _dbContext.SaveChangesAsync(cancellationToken);

            _observability.LogMigrationUnitStarted(
                unit.MigrationRun.RunIdentifier,
                unit.UnitIdentifier,
                unit.UnitType,
                unit.UnitName,
                unit.ExecutionOrder,
                unit.MigrationRun.ExecutionMode.ToString());
            _observability.RecordMigrationUnitStatus(unit.Status.ToString(), unit.UnitType);

            return unit;
        }

        if (unit.Status is not OnboardingMigrationUnitStatus.Pending and not OnboardingMigrationUnitStatus.Running)
        {
            throw new InvalidOperationException("Only pending or running migration units can transition to a terminal state.");
        }

        ArgumentNullException.ThrowIfNull(outcome);

        var finishedAtUtc = DateTime.UtcNow;
        unit.StartedAtUtc ??= finishedAtUtc;
        unit.FinishedAtUtc = finishedAtUtc;
        unit.Status = targetStatus;
        unit.ProcessedEntityCount = outcome.ProcessedEntityCount;
        unit.SucceededEntityCount = outcome.SucceededEntityCount;
        unit.FailedEntityCount = outcome.FailedEntityCount;
        unit.SkippedEntityCount = outcome.SkippedEntityCount;
        unit.UpdatedAtUtc = finishedAtUtc;
        unit.MigrationRun.UpdatedAtUtc = finishedAtUtc;

        if (unit.MigrationRun.Status == OnboardingMigrationRunStatus.NotStarted)
        {
            unit.MigrationRun.StartedAtUtc = unit.StartedAtUtc;
            unit.MigrationRun.Status = OnboardingMigrationRunStatus.Running;
        }

        await _dbContext.SaveChangesAsync(cancellationToken);

        var durationMilliseconds = unit.FinishedAtUtc.Value - unit.StartedAtUtc.Value;
        if (targetStatus == OnboardingMigrationUnitStatus.Failed)
        {
            _observability.LogMigrationUnitFailed(
                unit.MigrationRun.RunIdentifier,
                unit.UnitIdentifier,
                unit.UnitType,
                unit.UnitName,
                outcome.FailedEntityCount,
                outcome.ProcessedEntityCount);
        }
        else
        {
            _observability.LogMigrationUnitCompleted(
                unit.MigrationRun.RunIdentifier,
                unit.UnitIdentifier,
                unit.UnitType,
                unit.UnitName,
                targetStatus.ToString(),
                outcome.ProcessedEntityCount,
                outcome.SucceededEntityCount,
                outcome.FailedEntityCount,
                outcome.SkippedEntityCount);
        }

        _observability.RecordMigrationUnitStatus(unit.Status.ToString(), unit.UnitType);
        _observability.RecordMigrationUnitDuration(unit.UnitType, unit.Status.ToString(), durationMilliseconds.TotalMilliseconds);

        return unit;
    }

    private async Task<MigrationRun> GetRunAsync(Guid runIdentifier, CancellationToken cancellationToken)
        => await _dbContext.OnboardingMigrationRuns
            .SingleAsync(entity => entity.RunIdentifier == runIdentifier, cancellationToken);

    private static void EnsureRunIsMutable(MigrationRun run)
    {
        if (run.Status == OnboardingMigrationRunStatus.Cancelled || run.FinishedAtUtc.HasValue)
        {
            throw new InvalidOperationException("Migration run is no longer mutable.");
        }
    }

    private static OnboardingMigrationRunStatus DetermineRunStatus(IEnumerable<MigrationUnit> units)
    {
        var unitList = units.ToList();
        if (unitList.Count == 0)
        {
            return OnboardingMigrationRunStatus.Succeeded;
        }

        var failedCount = unitList.Count(unit => unit.Status == OnboardingMigrationUnitStatus.Failed);
        if (failedCount == 0)
        {
            return OnboardingMigrationRunStatus.Succeeded;
        }

        return failedCount == unitList.Count
            ? OnboardingMigrationRunStatus.Failed
            : OnboardingMigrationRunStatus.PartiallySucceeded;
    }

    private static void RefreshRunSummary(MigrationRun run)
    {
        var units = run.Units.ToList();
        var issues = run.Issues.ToList();

        run.TotalUnitCount = units.Count;
        run.SucceededUnitCount = units.Count(unit => unit.Status == OnboardingMigrationUnitStatus.Succeeded);
        run.FailedUnitCount = units.Count(unit => unit.Status == OnboardingMigrationUnitStatus.Failed);
        run.SkippedUnitCount = units.Count(unit => unit.Status == OnboardingMigrationUnitStatus.Skipped);
        run.ProcessedEntityCount = units.Sum(unit => unit.ProcessedEntityCount);
        run.SucceededEntityCount = units.Sum(unit => unit.SucceededEntityCount);
        run.FailedEntityCount = units.Sum(unit => unit.FailedEntityCount);
        run.SkippedEntityCount = units.Sum(unit => unit.SkippedEntityCount);
        run.IssueCount = issues.Count;
        run.BlockingIssueCount = issues.Count(issue => issue.IsBlocking);
        run.UpdatedAtUtc = DateTime.UtcNow;
    }

    private void LogRunFinalized(MigrationRun run)
    {
        _observability.LogMigrationRunFinalized(
            run.RunIdentifier,
            run.Status.ToString(),
            run.TotalUnitCount,
            run.SucceededUnitCount,
            run.FailedUnitCount,
            run.SkippedUnitCount,
            run.IssueCount,
            run.BlockingIssueCount);
        _observability.RecordMigrationRunStatus(run.Status.ToString(), run.ExecutionMode.ToString());

        if (run.StartedAtUtc.HasValue && run.FinishedAtUtc.HasValue)
        {
            var duration = run.FinishedAtUtc.Value - run.StartedAtUtc.Value;
            _observability.RecordMigrationRunDuration(run.Status.ToString(), run.ExecutionMode.ToString(), duration.TotalMilliseconds);
        }
    }

    private static OnboardingMigrationRunSummary MapSummary(MigrationRun run)
        => new(
            run.RunIdentifier,
            run.MigrationVersion,
            run.EnvironmentRing,
            run.TriggerType,
            run.ExecutionMode,
            run.Status,
            run.StartedAtUtc,
            run.FinishedAtUtc,
            run.TotalUnitCount,
            run.SucceededUnitCount,
            run.FailedUnitCount,
            run.SkippedUnitCount,
            run.ProcessedEntityCount,
            run.SucceededEntityCount,
            run.FailedEntityCount,
            run.SkippedEntityCount,
            run.IssueCount,
            run.BlockingIssueCount,
            run.Units
                .OrderBy(unit => unit.ExecutionOrder)
                .Select(unit => new OnboardingMigrationUnitSummary(
                    unit.UnitIdentifier,
                    unit.UnitType,
                    unit.UnitName,
                    unit.ExecutionOrder,
                    unit.Status,
                    unit.StartedAtUtc,
                    unit.FinishedAtUtc,
                    unit.ProcessedEntityCount,
                    unit.SucceededEntityCount,
                    unit.FailedEntityCount,
                    unit.SkippedEntityCount))
                .ToArray(),
            run.Issues
                .OrderBy(issue => issue.CreatedAtUtc)
                .Select(issue => new OnboardingMigrationIssueSummary(
                    issue.IssueIdentifier,
                    run.RunIdentifier,
                    issue.MigrationUnit?.UnitIdentifier,
                    issue.IssueType,
                    issue.IssueCategory,
                    issue.Severity,
                    issue.SourceLegacyReference,
                    issue.TargetEntityType,
                    issue.TargetExternalIdentity,
                    issue.SanitizedMessage,
                    issue.SanitizedDetails,
                    issue.IsBlocking,
                    issue.CreatedAtUtc))
                .ToArray());
}
