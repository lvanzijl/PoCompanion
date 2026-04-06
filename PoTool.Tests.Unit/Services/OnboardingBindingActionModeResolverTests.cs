using PoTool.Client.Models;
using PoTool.Client.Services;
using PoTool.Shared.Onboarding;

namespace PoTool.Tests.Unit.Services;

[TestClass]
public sealed class OnboardingBindingActionModeResolverTests
{
    [TestMethod]
    public void Resolve_WhenIntentIsReplacement_ReturnsReplaceMode()
    {
        var binding = CreateBinding(OnboardingProductSourceTypeDto.Team);

        var mode = OnboardingBindingActionModeResolver.Resolve(
            CreateIntent("replace-binding-source", bindingId: 4),
            binding);

        Assert.AreEqual(OnboardingBindingActionMode.Replace, mode);
    }

    [TestMethod]
    public void Resolve_WhenBindingIsMissing_ReturnsCreateMode()
    {
        var mode = OnboardingBindingActionModeResolver.Resolve(CreateIntent("create-binding"), null);

        Assert.AreEqual(OnboardingBindingActionMode.Create, mode);
    }

    [TestMethod]
    public void Resolve_WhenExistingBindingUsesCreateIntent_DoesNotFallIntoReplaceMode()
    {
        var binding = CreateBinding(OnboardingProductSourceTypeDto.Project);

        var mode = OnboardingBindingActionModeResolver.Resolve(
            CreateIntent("create-binding", bindingId: 4),
            binding);

        Assert.AreEqual(OnboardingBindingActionMode.Update, mode);
    }

    private static ExecutionIntentViewModel CreateIntent(string intentType, int? bindingId = null)
        => new(
            intentType,
            OnboardingProblemScope.Binding,
            1,
            2,
            3,
            bindingId,
            "Test intent",
            OnboardingExecutionConfidenceLevel.High,
            new ExecutionIntentNavigationTargetViewModel("/home/onboarding", "section-bindings", OnboardingGraphSection.Bindings, bindingId.HasValue ? $"binding-{bindingId.Value}" : "root-3", [OnboardingGraphSection.Bindings]));

    private static OnboardingProductSourceBindingDto CreateBinding(OnboardingProductSourceTypeDto sourceType)
        => new(
            4,
            3,
            2,
            sourceType == OnboardingProductSourceTypeDto.Team ? 9 : null,
            sourceType == OnboardingProductSourceTypeDto.Pipeline ? 10 : null,
            sourceType,
            sourceType == OnboardingProductSourceTypeDto.Pipeline ? "9102" : "team-2",
            true,
            new OnboardingValidationStateDto(OnboardingValidationStatus.Valid, DateTime.UtcNow, OnboardingValidationSource.Live, null, null, [], null, null, null),
            new OnboardingEntityStatusDto(OnboardingConfigurationStatus.PartiallyConfigured, [], []),
            new OnboardingAuditDto(DateTime.UtcNow, DateTime.UtcNow, null, null));
}
