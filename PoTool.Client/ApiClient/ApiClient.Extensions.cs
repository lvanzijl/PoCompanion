using System.Text.Json;

namespace PoTool.Client.ApiClient;

/// <summary>
/// Extends each NSwag-generated client to enable case-insensitive JSON property name matching.
///
/// The generated clients use System.Text.Json with default (case-sensitive) options, but ASP.NET Core
/// serialises response bodies with camelCase property names. Shared DTOs (excluded from code generation
/// and therefore lacking [JsonPropertyName] attributes) fail to deserialise unless case-insensitive
/// matching is enabled. Examples: ProductBacklogStateDto, PortfolioProgressTrendDto, BugTriageStateDto.
/// </summary>
internal static class ApiClientJsonSettings
{
    internal static void Configure(JsonSerializerOptions settings)
    {
        settings.PropertyNameCaseInsensitive = true;
    }
}

public partial class Client
{
    static partial void UpdateJsonSerializerSettings(JsonSerializerOptions settings)
        => ApiClientJsonSettings.Configure(settings);
}

public partial class CacheSyncClient
{
    static partial void UpdateJsonSerializerSettings(JsonSerializerOptions settings)
        => ApiClientJsonSettings.Configure(settings);
}

public partial class DataSourceModeClient
{
    static partial void UpdateJsonSerializerSettings(JsonSerializerOptions settings)
        => ApiClientJsonSettings.Configure(settings);
}

public partial class FilteringClient
{
    static partial void UpdateJsonSerializerSettings(JsonSerializerOptions settings)
        => ApiClientJsonSettings.Configure(settings);
}

public partial class HealthCalculationClient
{
    static partial void UpdateJsonSerializerSettings(JsonSerializerOptions settings)
        => ApiClientJsonSettings.Configure(settings);
}

public partial class MetricsClient
{
    static partial void UpdateJsonSerializerSettings(JsonSerializerOptions settings)
        => ApiClientJsonSettings.Configure(settings);
}

public partial class PipelinesClient
{
    static partial void UpdateJsonSerializerSettings(JsonSerializerOptions settings)
        => ApiClientJsonSettings.Configure(settings);
}

public partial class ProductsClient
{
    static partial void UpdateJsonSerializerSettings(JsonSerializerOptions settings)
        => ApiClientJsonSettings.Configure(settings);
}

public partial class ProfilesClient
{
    static partial void UpdateJsonSerializerSettings(JsonSerializerOptions settings)
        => ApiClientJsonSettings.Configure(settings);
}

public partial class PullRequestsClient
{
    static partial void UpdateJsonSerializerSettings(JsonSerializerOptions settings)
        => ApiClientJsonSettings.Configure(settings);
}

public partial class ReleasePlanningClient
{
    static partial void UpdateJsonSerializerSettings(JsonSerializerOptions settings)
        => ApiClientJsonSettings.Configure(settings);
}

public partial class SettingsClient
{
    static partial void UpdateJsonSerializerSettings(JsonSerializerOptions settings)
        => ApiClientJsonSettings.Configure(settings);
}

public partial class SprintsClient
{
    static partial void UpdateJsonSerializerSettings(JsonSerializerOptions settings)
        => ApiClientJsonSettings.Configure(settings);
}

public partial class StartupClient
{
    static partial void UpdateJsonSerializerSettings(JsonSerializerOptions settings)
        => ApiClientJsonSettings.Configure(settings);
}

public partial class TeamsClient
{
    static partial void UpdateJsonSerializerSettings(JsonSerializerOptions settings)
        => ApiClientJsonSettings.Configure(settings);
}

public partial class WorkItemsClient
{
    static partial void UpdateJsonSerializerSettings(JsonSerializerOptions settings)
        => ApiClientJsonSettings.Configure(settings);
}

public partial class BugTriageClient
{
    static partial void UpdateJsonSerializerSettings(JsonSerializerOptions settings)
        => ApiClientJsonSettings.Configure(settings);
}
