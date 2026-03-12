using PoTool.Shared.Settings;

namespace PoTool.Client.Helpers;

public static class ConfigurationImportResultDisplayHelper
{
    public static bool IsCleanSingleProfileSuccess(ConfigurationImportResultDto? result)
    {
        return result is
        {
            ImportExecuted: true,
            ProfilesImported.Count: 1,
            Warnings.Count: 0,
            Errors.Count: 0,
            RemovedItems.Count: 0
        };
    }

    public static bool ShouldShowDetailedResult(ConfigurationImportResultDto? result)
    {
        if (result == null)
        {
            return false;
        }

        if (!result.ImportExecuted)
        {
            return result.Warnings.Count > 0
                || result.Errors.Count > 0
                || result.ExistingConfigurationSummary.Count > 0;
        }

        return !IsCleanSingleProfileSuccess(result);
    }
}
