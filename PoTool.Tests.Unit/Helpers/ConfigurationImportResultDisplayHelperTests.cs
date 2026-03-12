using PoTool.Client.Helpers;
using PoTool.Shared.Settings;

namespace PoTool.Tests.Unit.Helpers;

[TestClass]
public sealed class ConfigurationImportResultDisplayHelperTests
{
    [TestMethod]
    public void IsCleanSingleProfileSuccess_ReturnsTrue_ForSingleProfileWithoutWarningsErrorsOrRemovedItems()
    {
        var result = CreateResult(
            importExecuted: true,
            profilesImported: ["Lesley"]);

        Assert.IsTrue(ConfigurationImportResultDisplayHelper.IsCleanSingleProfileSuccess(result));
        Assert.IsFalse(ConfigurationImportResultDisplayHelper.ShouldShowDetailedResult(result));
    }

    [TestMethod]
    public void ShouldShowDetailedResult_ReturnsTrue_WhenRemovedItemsExist()
    {
        var result = CreateResult(
            importExecuted: true,
            profilesImported: ["Lesley"],
            removedItems: ["Removed 1 profile(s)."]);

        Assert.IsTrue(ConfigurationImportResultDisplayHelper.ShouldShowDetailedResult(result));
    }

    [TestMethod]
    public void ShouldShowDetailedResult_ReturnsTrue_WhenWarningsExistAfterImport()
    {
        var result = CreateResult(
            importExecuted: true,
            profilesImported: ["Lesley"],
            warnings: ["Team link was skipped."]);

        Assert.IsTrue(ConfigurationImportResultDisplayHelper.ShouldShowDetailedResult(result));
    }

    [TestMethod]
    public void ShouldShowDetailedResult_ReturnsTrue_WhenImportDidNotExecuteAndErrorsExist()
    {
        var result = CreateResult(
            canImport: false,
            importExecuted: false,
            errors: ["Repository 'Repo-A' was not accessible."]);

        Assert.IsTrue(ConfigurationImportResultDisplayHelper.ShouldShowDetailedResult(result));
        Assert.IsFalse(ConfigurationImportResultDisplayHelper.IsCleanSingleProfileSuccess(result));
    }

    private static ConfigurationImportResultDto CreateResult(
        bool canImport = true,
        bool importExecuted = true,
        IReadOnlyList<string>? profilesImported = null,
        IReadOnlyList<string>? removedItems = null,
        IReadOnlyList<string>? warnings = null,
        IReadOnlyList<string>? errors = null)
    {
        return new ConfigurationImportResultDto(
            CanImport: canImport,
            ImportExecuted: importExecuted,
            ExistingConfigurationDetected: removedItems?.Count > 0,
            RequiresDestructiveConfirmation: false,
            ProfilesValidated: [],
            ProfilesImported: profilesImported ?? [],
            ExistingConfigurationSummary: [],
            RemovedItems: removedItems ?? [],
            Warnings: warnings ?? [],
            Errors: errors ?? []);
    }
}
