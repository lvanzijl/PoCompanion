using System.Net.Http.Json;
using System.Text.Json;
using PoTool.Shared.Settings;

namespace PoTool.Client.Services;

public sealed class ConfigurationTransferService
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        PropertyNameCaseInsensitive = true,
        WriteIndented = true
    };

    private readonly HttpClient _httpClient;

    public ConfigurationTransferService(HttpClient httpClient)
    {
        _httpClient = httpClient;
    }

    public async Task<ConfigurationExportDto> ExportAsync(CancellationToken cancellationToken = default)
    {
        var export = await _httpClient.GetFromJsonAsync<ConfigurationExportDto>(
            "/api/settings/configuration-export",
            JsonOptions,
            cancellationToken);

        return export ?? throw new InvalidOperationException("The export endpoint returned no configuration.");
    }

    public async Task<ConfigurationImportResultDto> ImportAsync(
        string jsonContent,
        bool wipeExistingConfiguration = false,
        CancellationToken cancellationToken = default)
    {
        return await SendImportRequestAsync(
            "/api/settings/configuration-import",
            new ConfigurationImportRequest(jsonContent, ValidateOnly: false, WipeExistingConfiguration: wipeExistingConfiguration),
            cancellationToken);
    }

    public string Serialize(ConfigurationExportDto exportConfiguration)
    {
        return JsonSerializer.Serialize(exportConfiguration, JsonOptions);
    }

    private async Task<ConfigurationImportResultDto> SendImportRequestAsync(
        string url,
        ConfigurationImportRequest request,
        CancellationToken cancellationToken)
    {
        var response = await _httpClient.PostAsJsonAsync(url, request, JsonOptions, cancellationToken);
        response.EnsureSuccessStatusCode();

        var result = await response.Content.ReadFromJsonAsync<ConfigurationImportResultDto>(JsonOptions, cancellationToken);
        return result ?? throw new InvalidOperationException("The configuration import endpoint returned no result.");
    }
}
