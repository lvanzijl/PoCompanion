using PoTool.Client.Models;
using PoTool.Shared.Onboarding;

namespace PoTool.Client.Services;

public enum OnboardingBindingActionMode
{
    Create,
    Update,
    Replace
}

public static class OnboardingBindingActionModeResolver
{
    public static OnboardingBindingActionMode Resolve(ExecutionIntentViewModel? activeIntent, OnboardingProductSourceBindingDto? binding)
    {
        if (string.Equals(activeIntent?.IntentType, "replace-binding-source", StringComparison.Ordinal))
        {
            return OnboardingBindingActionMode.Replace;
        }

        return binding is null
            ? OnboardingBindingActionMode.Create
            : OnboardingBindingActionMode.Update;
    }

    public static bool SupportsReplacement(OnboardingProductSourceBindingDto? binding)
        => binding?.SourceType is OnboardingProductSourceTypeDto.Team or OnboardingProductSourceTypeDto.Pipeline;
}
