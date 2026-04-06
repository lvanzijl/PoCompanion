using PoTool.Api.Persistence.Entities.Onboarding;
using PoTool.Api.Services.Onboarding;

namespace PoTool.Api.Handlers.Onboarding;

public interface IOnboardingMigrationJobHandler
{
    Task<OnboardingMigrationRunSummary> RunDryRunAsync(OnboardingMigrationDryRunRequest request, CancellationToken cancellationToken);
}

public sealed class OnboardingMigrationJobHandler : IOnboardingMigrationJobHandler
{
    private readonly IOnboardingMigrationLedgerService _migrationLedgerService;

    public OnboardingMigrationJobHandler(IOnboardingMigrationLedgerService migrationLedgerService)
    {
        _migrationLedgerService = migrationLedgerService;
    }

    public async Task<OnboardingMigrationRunSummary> RunDryRunAsync(
        OnboardingMigrationDryRunRequest request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        var run = await _migrationLedgerService.CreateRunAsync(
            new OnboardingMigrationRunCreateRequest(
                request.MigrationVersion,
                request.EnvironmentRing,
                request.TriggerType,
                OnboardingMigrationExecutionMode.DryRun,
                request.SourceFingerprint),
            cancellationToken);

        var createdUnits = await _migrationLedgerService.CreateUnitsAsync(
            run.RunIdentifier,
            request.Units
                .Select(unit => new OnboardingMigrationUnitPlan(unit.UnitType, unit.UnitName, unit.ExecutionOrder))
                .ToArray(),
            cancellationToken);

        foreach (var pair in createdUnits
                     .Join(
                         request.Units,
                         created => created.ExecutionOrder,
                         planned => planned.ExecutionOrder,
                         (created, planned) => new { created, planned })
                     .OrderBy(item => item.created.ExecutionOrder))
        {
            await _migrationLedgerService.StartUnitAsync(pair.created.UnitIdentifier, cancellationToken);

            foreach (var issue in pair.planned.Issues)
            {
                await _migrationLedgerService.RecordIssueAsync(
                    run.RunIdentifier,
                    new OnboardingMigrationIssueCreateRequest(
                        pair.created.UnitIdentifier,
                        issue.IssueType,
                        issue.IssueCategory,
                        issue.Severity,
                        issue.SourceLegacyReference,
                        issue.TargetEntityType,
                        issue.TargetExternalIdentity,
                        issue.SanitizedMessage,
                        issue.SanitizedDetails,
                        issue.IsBlocking),
                    cancellationToken);
            }

            switch (pair.planned.FinalStatus)
            {
                case OnboardingMigrationUnitStatus.Succeeded:
                    await _migrationLedgerService.CompleteUnitAsync(
                        pair.created.UnitIdentifier,
                        pair.planned.Outcome,
                        cancellationToken);
                    break;
                case OnboardingMigrationUnitStatus.Failed:
                    await _migrationLedgerService.FailUnitAsync(
                        pair.created.UnitIdentifier,
                        pair.planned.Outcome,
                        cancellationToken);
                    break;
                case OnboardingMigrationUnitStatus.Skipped:
                    await _migrationLedgerService.SkipUnitAsync(
                        pair.created.UnitIdentifier,
                        pair.planned.Outcome,
                        cancellationToken);
                    break;
                default:
                    throw new InvalidOperationException("Dry-run units must end in Succeeded, Failed, or Skipped status.");
            }
        }

        await _migrationLedgerService.FinalizeRunAsync(run.RunIdentifier, cancellationToken);
        return await _migrationLedgerService.GetRunSummaryAsync(run.RunIdentifier, cancellationToken);
    }
}
