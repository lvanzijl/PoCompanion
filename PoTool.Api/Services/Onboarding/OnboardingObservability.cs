using System.Diagnostics.Metrics;
using Microsoft.Extensions.Logging;
using PoTool.Shared.Onboarding;

namespace PoTool.Api.Services.Onboarding;

public interface IOnboardingObservability
{
    void RecordLookupCompleted(string operation, bool success, OnboardingErrorCode? errorCode);
    void RecordValidationCompleted(string entityType, bool success, OnboardingErrorCode? errorCode);
    void LogLookupStarted(string operation);
    void LogLookupCompleted(string operation, bool success, OnboardingErrorCode? errorCode);
    void LogValidationCompleted(string entityType, bool success, OnboardingErrorCode? errorCode);
}

public sealed class OnboardingObservability : IOnboardingObservability
{
    private static readonly Meter Meter = new("PoTool.Onboarding");
    private static readonly Counter<long> LookupCount = Meter.CreateCounter<long>("onboarding.lookup.count");
    private static readonly Counter<long> LookupFailureCount = Meter.CreateCounter<long>("onboarding.lookup.failure.count");
    private static readonly Counter<long> ValidationCount = Meter.CreateCounter<long>("onboarding.validation.count");
    private static readonly Counter<long> ValidationFailureCount = Meter.CreateCounter<long>("onboarding.validation.failure.count");

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
}
