using System.Diagnostics.Metrics;
using Microsoft.Extensions.Logging;
using PoTool.Shared.Onboarding;

namespace PoTool.Api.Services.Onboarding;

public interface IOnboardingObservability
{
    void RecordLookupCompleted(string operation, bool success, OnboardingErrorCode? errorCode);
    void RecordValidationCompleted(string entityType, bool success, OnboardingErrorCode? errorCode);
    void RecordStatusComputed(string overallStatus, int blockerCount, int warningCount);
    void LogLookupStarted(string operation);
    void LogLookupCompleted(string operation, bool success, OnboardingErrorCode? errorCode);
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
}
