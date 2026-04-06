using PoTool.Client.Models;
using PoTool.Shared.Onboarding;

namespace PoTool.Client.Services;

internal static class OnboardingSignalPresentation
{
    internal static bool TryMapValidationSeverity(
        OnboardingValidationStatus status,
        out OnboardingProblemSeverity severity)
    {
        switch (status)
        {
            case OnboardingValidationStatus.Invalid:
            case OnboardingValidationStatus.PermissionDenied:
            case OnboardingValidationStatus.CapabilityDenied:
                severity = OnboardingProblemSeverity.Blocking;
                return true;
            case OnboardingValidationStatus.Unavailable:
                severity = OnboardingProblemSeverity.Warning;
                return true;
            default:
                severity = default;
                return false;
        }
    }

    internal static string BuildValidationReason(OnboardingValidationStateDto validationState)
    {
        var statusReason = validationState.Status switch
        {
            OnboardingValidationStatus.Invalid => "Validation failed against the current external source.",
            OnboardingValidationStatus.Unavailable => "The external source is currently unavailable.",
            OnboardingValidationStatus.PermissionDenied => "Permissions are insufficient for the required onboarding reads.",
            OnboardingValidationStatus.CapabilityDenied => "The connection does not expose all required capabilities.",
            _ => "Validation requires attention."
        };

        return string.IsNullOrWhiteSpace(validationState.ErrorMessageSanitized)
            ? statusReason
            : $"{statusReason} {validationState.ErrorMessageSanitized}".Trim();
    }

    internal static string BuildValidationTitle(string affectedEntity, OnboardingValidationStatus status)
        => status switch
        {
            OnboardingValidationStatus.PermissionDenied => $"{affectedEntity} is blocked by missing permissions",
            OnboardingValidationStatus.CapabilityDenied => $"{affectedEntity} is blocked by missing capabilities",
            OnboardingValidationStatus.Unavailable => $"{affectedEntity} depends on an unavailable source",
            OnboardingValidationStatus.Invalid => $"{affectedEntity} has a validation issue",
            _ => $"{affectedEntity} requires attention"
        };

    internal static OnboardingProblemSeverity? ResolveEntityProblemSeverity(
        OnboardingEntityStatusDto status,
        params OnboardingValidationStateDto?[] validationStates)
    {
        if (status.BlockingReasons.Count > 0)
        {
            return OnboardingProblemSeverity.Blocking;
        }

        if (validationStates.Any(validationState => validationState is not null
                                                    && TryMapValidationSeverity(validationState.Status, out var severity)
                                                    && severity == OnboardingProblemSeverity.Blocking))
        {
            return OnboardingProblemSeverity.Blocking;
        }

        if (status.Warnings.Count > 0)
        {
            return OnboardingProblemSeverity.Warning;
        }

        if (validationStates.Any(validationState => validationState is not null
                                                    && TryMapValidationSeverity(validationState.Status, out var severity)
                                                    && severity == OnboardingProblemSeverity.Warning))
        {
            return OnboardingProblemSeverity.Warning;
        }

        return null;
    }

    internal static string? BuildEntityProblemSummary(
        OnboardingEntityStatusDto status,
        params OnboardingValidationStateDto?[] validationStates)
    {
        if (status.BlockingReasons.Count > 0)
        {
            return $"Blocked because {status.BlockingReasons[0].Message}";
        }

        foreach (var validationState in validationStates.Where(state => state is not null))
        {
            if (TryMapValidationSeverity(validationState!.Status, out var severity)
                && severity == OnboardingProblemSeverity.Blocking)
            {
                return $"Blocked because {BuildValidationReason(validationState)}";
            }
        }

        if (status.Warnings.Count > 0)
        {
            return $"Warning because {status.Warnings[0].Message}";
        }

        foreach (var validationState in validationStates.Where(state => state is not null))
        {
            if (TryMapValidationSeverity(validationState!.Status, out var severity)
                && severity == OnboardingProblemSeverity.Warning)
            {
                return $"Warning because {BuildValidationReason(validationState)}";
            }
        }

        return null;
    }
}
