using System.Diagnostics.Metrics;
using Microsoft.Extensions.Logging;
using PoTool.Shared.Onboarding;

namespace PoTool.Api.Services.Onboarding;

public interface IOnboardingObservability
{
    void RecordLookupCompleted(string operation, bool success, OnboardingErrorCode? errorCode);
    void RecordMigrationIssue(string severity, string category, bool isBlocking);
    void RecordMigrationRunDuration(string status, string executionMode, double durationMilliseconds);
    void RecordMigrationRunStatus(string status, string executionMode);
    void RecordMigrationUnitDuration(string unitType, string status, double durationMilliseconds);
    void RecordMigrationUnitStatus(string status, string unitType);
    void RecordValidationCompleted(string entityType, bool success, OnboardingErrorCode? errorCode);
    void RecordStatusComputed(string overallStatus, int blockerCount, int warningCount);
    void LogLookupStarted(string operation);
    void LogLookupCompleted(string operation, bool success, OnboardingErrorCode? errorCode);
    void LogMigrationIssueRecorded(Guid runIdentifier, Guid? unitIdentifier, string category, string severity, bool isBlocking);
    void LogMigrationRunFinalized(Guid runIdentifier, string status, int totalUnits, int succeededUnits, int failedUnits, int skippedUnits, int issueCount, int blockingIssueCount);
    void LogMigrationRunStarted(Guid runIdentifier, string migrationVersion, string environmentRing, string executionMode, string triggerType);
    void LogMigrationUnitCompleted(Guid runIdentifier, Guid unitIdentifier, string unitType, string unitName, string status, int processedCount, int succeededCount, int failedCount, int skippedCount);
    void LogMigrationUnitFailed(Guid runIdentifier, Guid unitIdentifier, string unitType, string unitName, int failedCount, int processedCount);
    void LogMigrationUnitStarted(Guid runIdentifier, Guid unitIdentifier, string unitType, string unitName, int executionOrder, string executionMode);
    void LogStatusComputed(string overallStatus, int blockerCount, int warningCount);
    void LogStatusIssue(string severity, string code, string? entityType, string? entityExternalId);
    void LogValidationCompleted(string entityType, bool success, OnboardingErrorCode? errorCode);
}

public sealed class OnboardingObservability : IOnboardingObservability
{
    private static readonly Meter Meter = new("PoTool.Onboarding");
    private static readonly Counter<long> LookupCount = Meter.CreateCounter<long>("onboarding.lookup.count");
    private static readonly Counter<long> LookupFailureCount = Meter.CreateCounter<long>("onboarding.lookup.failure.count");
    private static readonly Counter<long> ValidationCount = Meter.CreateCounter<long>("onboarding.validation.count");
    private static readonly Counter<long> ValidationFailureCount = Meter.CreateCounter<long>("onboarding.validation.failure.count");
    private static readonly Counter<long> StatusComputationCount = Meter.CreateCounter<long>("onboarding.status.count");
    private static readonly Histogram<long> StatusBlockerCount = Meter.CreateHistogram<long>("onboarding.status.blocker.count");
    private static readonly Histogram<long> StatusWarningCount = Meter.CreateHistogram<long>("onboarding.status.warning.count");
    private static readonly Counter<long> MigrationRunCount = Meter.CreateCounter<long>("onboarding.migration.run.count");
    private static readonly Counter<long> MigrationUnitCount = Meter.CreateCounter<long>("onboarding.migration.unit.count");
    private static readonly Counter<long> MigrationIssueCount = Meter.CreateCounter<long>("onboarding.migration.issue.count");
    private static readonly Histogram<double> MigrationRunDuration = Meter.CreateHistogram<double>("onboarding.migration.run.duration.ms");
    private static readonly Histogram<double> MigrationUnitDuration = Meter.CreateHistogram<double>("onboarding.migration.unit.duration.ms");

    private readonly ILogger<OnboardingObservability> _logger;

    public OnboardingObservability(ILogger<OnboardingObservability> logger)
    {
        _logger = logger;
    }

    public void LogLookupStarted(string operation)
    {
        _logger.LogInformation("Onboarding lookup started. Operation={Operation}", operation);
    }

    public void LogLookupCompleted(string operation, bool success, OnboardingErrorCode? errorCode)
    {
        _logger.LogInformation(
            "Onboarding lookup completed. Operation={Operation} Outcome={Outcome} ErrorCode={ErrorCode}",
            operation,
            success ? "Success" : "Failure",
            errorCode?.ToString() ?? "None");
    }

    public void LogValidationCompleted(string entityType, bool success, OnboardingErrorCode? errorCode)
    {
        _logger.LogInformation(
            "Onboarding revalidation completed. EntityType={EntityType} Outcome={Outcome} ErrorCode={ErrorCode}",
            entityType,
            success ? "Success" : "Failure",
            errorCode?.ToString() ?? "None");
    }

    public void LogStatusComputed(string overallStatus, int blockerCount, int warningCount)
    {
        _logger.LogInformation(
            "Onboarding status computed. OverallStatus={OverallStatus} BlockerCount={BlockerCount} WarningCount={WarningCount}",
            overallStatus,
            blockerCount,
            warningCount);
    }

    public void LogStatusIssue(string severity, string code, string? entityType, string? entityExternalId)
    {
        _logger.LogInformation(
            "Onboarding status issue detected. Severity={Severity} Code={Code} EntityType={EntityType} EntityExternalId={EntityExternalId}",
            severity,
            code,
            entityType ?? "None",
            entityExternalId ?? "None");
    }

    public void LogMigrationRunStarted(
        Guid runIdentifier,
        string migrationVersion,
        string environmentRing,
        string executionMode,
        string triggerType)
    {
        _logger.LogInformation(
            "Onboarding migration run started. RunIdentifier={RunIdentifier} MigrationVersion={MigrationVersion} EnvironmentRing={EnvironmentRing} ExecutionMode={ExecutionMode} TriggerType={TriggerType}",
            runIdentifier,
            migrationVersion,
            environmentRing,
            executionMode,
            triggerType);
    }

    public void LogMigrationUnitStarted(
        Guid runIdentifier,
        Guid unitIdentifier,
        string unitType,
        string unitName,
        int executionOrder,
        string executionMode)
    {
        _logger.LogInformation(
            "Onboarding migration unit started. RunIdentifier={RunIdentifier} UnitIdentifier={UnitIdentifier} UnitType={UnitType} UnitName={UnitName} ExecutionOrder={ExecutionOrder} ExecutionMode={ExecutionMode}",
            runIdentifier,
            unitIdentifier,
            unitType,
            unitName,
            executionOrder,
            executionMode);
    }

    public void LogMigrationUnitCompleted(
        Guid runIdentifier,
        Guid unitIdentifier,
        string unitType,
        string unitName,
        string status,
        int processedCount,
        int succeededCount,
        int failedCount,
        int skippedCount)
    {
        _logger.LogInformation(
            "Onboarding migration unit completed. RunIdentifier={RunIdentifier} UnitIdentifier={UnitIdentifier} UnitType={UnitType} UnitName={UnitName} Status={Status} ProcessedCount={ProcessedCount} SucceededCount={SucceededCount} FailedCount={FailedCount} SkippedCount={SkippedCount}",
            runIdentifier,
            unitIdentifier,
            unitType,
            unitName,
            status,
            processedCount,
            succeededCount,
            failedCount,
            skippedCount);
    }

    public void LogMigrationUnitFailed(
        Guid runIdentifier,
        Guid unitIdentifier,
        string unitType,
        string unitName,
        int failedCount,
        int processedCount)
    {
        _logger.LogInformation(
            "Onboarding migration unit failed. RunIdentifier={RunIdentifier} UnitIdentifier={UnitIdentifier} UnitType={UnitType} UnitName={UnitName} FailedCount={FailedCount} ProcessedCount={ProcessedCount}",
            runIdentifier,
            unitIdentifier,
            unitType,
            unitName,
            failedCount,
            processedCount);
    }

    public void LogMigrationIssueRecorded(
        Guid runIdentifier,
        Guid? unitIdentifier,
        string category,
        string severity,
        bool isBlocking)
    {
        _logger.LogInformation(
            "Onboarding migration issue recorded. RunIdentifier={RunIdentifier} UnitIdentifier={UnitIdentifier} Category={Category} Severity={Severity} IsBlocking={IsBlocking}",
            runIdentifier,
            unitIdentifier?.ToString() ?? "None",
            category,
            severity,
            isBlocking);
    }

    public void LogMigrationRunFinalized(
        Guid runIdentifier,
        string status,
        int totalUnits,
        int succeededUnits,
        int failedUnits,
        int skippedUnits,
        int issueCount,
        int blockingIssueCount)
    {
        _logger.LogInformation(
            "Onboarding migration run finalized. RunIdentifier={RunIdentifier} Status={Status} TotalUnits={TotalUnits} SucceededUnits={SucceededUnits} FailedUnits={FailedUnits} SkippedUnits={SkippedUnits} IssueCount={IssueCount} BlockingIssueCount={BlockingIssueCount}",
            runIdentifier,
            status,
            totalUnits,
            succeededUnits,
            failedUnits,
            skippedUnits,
            issueCount,
            blockingIssueCount);
    }

    public void RecordLookupCompleted(string operation, bool success, OnboardingErrorCode? errorCode)
    {
        LookupCount.Add(1, new KeyValuePair<string, object?>("operation", operation));
        if (!success)
        {
            LookupFailureCount.Add(
                1,
                new KeyValuePair<string, object?>("operation", operation),
                new KeyValuePair<string, object?>("error_code", errorCode?.ToString() ?? "Unknown"));
        }
    }

    public void RecordValidationCompleted(string entityType, bool success, OnboardingErrorCode? errorCode)
    {
        ValidationCount.Add(1, new KeyValuePair<string, object?>("entity_type", entityType));
        if (!success)
        {
            ValidationFailureCount.Add(
                1,
                new KeyValuePair<string, object?>("entity_type", entityType),
                new KeyValuePair<string, object?>("error_code", errorCode?.ToString() ?? "Unknown"));
        }
    }

    public void RecordStatusComputed(string overallStatus, int blockerCount, int warningCount)
    {
        StatusComputationCount.Add(1, new KeyValuePair<string, object?>("overall_status", overallStatus));
        StatusBlockerCount.Record(blockerCount, new KeyValuePair<string, object?>("overall_status", overallStatus));
        StatusWarningCount.Record(warningCount, new KeyValuePair<string, object?>("overall_status", overallStatus));
    }

    public void RecordMigrationRunStatus(string status, string executionMode)
    {
        MigrationRunCount.Add(
            1,
            new KeyValuePair<string, object?>("status", status),
            new KeyValuePair<string, object?>("execution_mode", executionMode));
    }

    public void RecordMigrationUnitStatus(string status, string unitType)
    {
        MigrationUnitCount.Add(
            1,
            new KeyValuePair<string, object?>("status", status),
            new KeyValuePair<string, object?>("unit_type", unitType));
    }

    public void RecordMigrationIssue(string severity, string category, bool isBlocking)
    {
        MigrationIssueCount.Add(
            1,
            new KeyValuePair<string, object?>("severity", severity),
            new KeyValuePair<string, object?>("category", category),
            new KeyValuePair<string, object?>("blocking", isBlocking));
    }

    public void RecordMigrationRunDuration(string status, string executionMode, double durationMilliseconds)
    {
        MigrationRunDuration.Record(
            durationMilliseconds,
            new KeyValuePair<string, object?>("status", status),
            new KeyValuePair<string, object?>("execution_mode", executionMode));
    }

    public void RecordMigrationUnitDuration(string unitType, string status, double durationMilliseconds)
    {
        MigrationUnitDuration.Record(
            durationMilliseconds,
            new KeyValuePair<string, object?>("unit_type", unitType),
            new KeyValuePair<string, object?>("status", status));
    }
}
