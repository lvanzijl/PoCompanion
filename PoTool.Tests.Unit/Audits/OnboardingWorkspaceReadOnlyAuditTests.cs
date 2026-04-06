namespace PoTool.Tests.Unit.Audits;

[TestClass]
public class OnboardingWorkspaceReadOnlyAuditTests
{
    private static readonly string[] WorkspaceFiles =
    [
        "/home/runner/work/PoCompanion/PoCompanion/PoTool.Client/Pages/Home/OnboardingWorkspace.razor",
        "/home/runner/work/PoCompanion/PoCompanion/PoTool.Client/Components/Onboarding/OnboardingEntityCard.razor",
        "/home/runner/work/PoCompanion/PoCompanion/PoTool.Client/Components/Onboarding/OnboardingProblemCard.razor",
        "/home/runner/work/PoCompanion/PoCompanion/PoTool.Client/Components/Onboarding/OnboardingRootCauseCard.razor",
        "/home/runner/work/PoCompanion/PoCompanion/PoTool.Client/Components/Onboarding/OnboardingFutureActionZone.razor",
        "/home/runner/work/PoCompanion/PoCompanion/PoTool.Client/Components/Onboarding/OnboardingStatusBadge.razor",
        "/home/runner/work/PoCompanion/PoCompanion/PoTool.Client/Components/Onboarding/OnboardingValidationBadge.razor",
        "/home/runner/work/PoCompanion/PoCompanion/PoTool.Client/Services/OnboardingExecutionIntentService.cs"
    ];

    [TestMethod]
    public void WorkspaceFiles_DoNotExposeWriteAffordances()
    {
        var forbiddenTerms = new[]
        {
            ">Create<",
            ">Update<",
            ">Delete<",
            ">Save<",
            ">Import<",
            ">Edit<",
            "Label=\"Create\"",
            "Label=\"Update\"",
            "Label=\"Delete\"",
            "Label=\"Save\"",
            "Label=\"Import\"",
            "Label=\"Edit\""
        };

        foreach (var file in WorkspaceFiles)
        {
            var content = File.ReadAllText(file);
            foreach (var term in forbiddenTerms)
            {
                Assert.IsFalse(content.Contains(term, StringComparison.Ordinal), $"Unexpected mutation affordance '{term}' found in {file}.");
            }
        }
    }

    [TestMethod]
    public void WorkspaceFiles_DoNotReferenceWizardOrDirectHttpClient()
    {
        foreach (var file in WorkspaceFiles)
        {
            var content = File.ReadAllText(file);
            Assert.IsFalse(content.Contains("OnboardingWizard", StringComparison.Ordinal), $"Wizard reference found in {file}.");
            Assert.IsFalse(content.Contains("IOnboardingWizardState", StringComparison.Ordinal), $"Wizard state reference found in {file}.");
            Assert.IsFalse(content.Contains("HttpClient", StringComparison.Ordinal), $"Direct HttpClient usage found in {file}.");
        }
    }

    [TestMethod]
    public void WorkspaceFiles_DoNotUseWriteEndpoints()
    {
        var forbiddenCalls = new[]
        {
            "PostAsJsonAsync",
            "PutAsJsonAsync",
            "PatchAsJsonAsync",
            "DeleteAsync",
            "SendAsync(",
            "POST",
            "PUT",
            "DELETE"
        };

        foreach (var file in WorkspaceFiles)
        {
            var content = File.ReadAllText(file);
            foreach (var call in forbiddenCalls)
            {
                Assert.IsFalse(content.Contains(call, StringComparison.Ordinal), $"Unexpected write call '{call}' found in {file}.");
            }
        }
    }
}
