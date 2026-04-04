using System.Text.Json;
using PoTool.Client.ApiClient;
using PoTool.Shared.Settings;

namespace PoTool.Client.Services;

public sealed class ConfigurationTransferService
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        PropertyNameCaseInsensitive = true,
        WriteIndented = true
    };

    private readonly ISettingsClient _settingsClient;

    public ConfigurationTransferService(ISettingsClient settingsClient)
    {
        _settingsClient = settingsClient;
    }

    public async Task<ConfigurationExportDto> ExportAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            return await _settingsClient.ExportConfigurationAsync(cancellationToken);
        }
        catch (ApiException ex) when (GeneratedClientErrorTranslator.IsSuccessfulEmptyResponse(ex))
        {
            throw new InvalidOperationException("The export endpoint returned no configuration.", ex);
        }
        catch (ApiException ex)
        {
            throw GeneratedClientErrorTranslator.ToHttpRequestException(ex);
        }
    }

    public async Task<ConfigurationImportResultDto> ImportAsync(
        string jsonContent,
        bool wipeExistingConfiguration = false,
        CancellationToken cancellationToken = default)
    {
        return await SendImportRequestAsync(
            new ConfigurationImportRequest(jsonContent, ValidateOnly: false, WipeExistingConfiguration: wipeExistingConfiguration),
            cancellationToken);
    }

    public string Serialize(ConfigurationExportDto exportConfiguration)
    {
        return JsonSerializer.Serialize(exportConfiguration, JsonOptions);
    }

    private async Task<ConfigurationImportResultDto> SendImportRequestAsync(
        ConfigurationImportRequest request,
        CancellationToken cancellationToken)
    {
        try
        {
            return await _settingsClient.ImportConfigurationAsync(request, cancellationToken);
        }
        catch (ApiException ex) when (GeneratedClientErrorTranslator.IsSuccessfulEmptyResponse(ex))
        {
            throw new InvalidOperationException("The configuration import endpoint returned no result.", ex);
        }
        catch (ApiException ex)
        {
            throw GeneratedClientErrorTranslator.ToHttpRequestException(ex);
        }
    }
}
