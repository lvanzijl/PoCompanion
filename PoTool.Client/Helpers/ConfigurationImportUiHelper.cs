using System.Text.Json;

namespace PoTool.Client.Helpers;

public static class ConfigurationImportUiHelper
{
    private const string InvalidConfigurationExportMessage = "The selected file is not a valid configuration export.";

    private static readonly string[] ImportStageMessages =
    [
        "Validating configuration",
        "Checking TFS connection",
        "Validating repositories and teams",
        "Importing configuration"
    ];

    public static bool CanImport(bool isBusy, string? selectedJson, string? validationError)
    {
        return !isBusy
            && !string.IsNullOrWhiteSpace(selectedJson)
            && string.IsNullOrWhiteSpace(validationError);
    }

    public static string GetImportStageMessage(int stageIndex)
    {
        if (stageIndex <= 0)
        {
            return ImportStageMessages[0];
        }

        if (stageIndex >= ImportStageMessages.Length)
        {
            return ImportStageMessages[^1];
        }

        return ImportStageMessages[stageIndex];
    }

    public static int GetImportStageMessageCount()
    {
        return ImportStageMessages.Length;
    }

    public static bool TryValidateSelectedJson(string? selectedJson, out string? validationError)
    {
        if (string.IsNullOrWhiteSpace(selectedJson))
        {
            validationError = InvalidConfigurationExportMessage;
            return false;
        }

        try
        {
            using var _ = JsonDocument.Parse(selectedJson);
            validationError = null;
            return true;
        }
        catch (JsonException)
        {
            validationError = InvalidConfigurationExportMessage;
            return false;
        }
    }
}
