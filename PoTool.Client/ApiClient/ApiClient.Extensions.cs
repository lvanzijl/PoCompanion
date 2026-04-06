using System.Text.Json;
using PoTool.Client.Helpers;
using PoTool.Client.Models;

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

internal static class CacheBackedGeneratedClientHelper
{
    internal static TData RequireData<TData>(
        this CacheBackedClientResult<TData> result,
        string operationName)
    {
        return result.State switch
        {
            CacheBackedClientState.Success when result.Data is not null => result.Data,
            CacheBackedClientState.Empty => throw new InvalidOperationException($"{operationName} returned an empty cache-backed payload."),
            CacheBackedClientState.NotReady => throw new InvalidOperationException($"{operationName} is not ready: {result.Reason ?? "Cache not ready."}"),
            CacheBackedClientState.Failed => throw new InvalidOperationException($"{operationName} failed: {result.Reason ?? "Cache-backed request failed."}"),
            CacheBackedClientState.Unavailable => throw new InvalidOperationException($"{operationName} is unavailable: {result.Reason ?? "Cache-backed request unavailable."}"),
            _ => throw new InvalidOperationException($"{operationName} did not contain usable cache-backed data.")
        };
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

public partial class ProjectsClient
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

public partial class OnboardingCrudClient
{
    static partial void UpdateJsonSerializerSettings(JsonSerializerOptions settings)
        => ApiClientJsonSettings.Configure(settings);
}

public partial class OnboardingLookupClient
{
    static partial void UpdateJsonSerializerSettings(JsonSerializerOptions settings)
        => ApiClientJsonSettings.Configure(settings);
}

public partial class OnboardingStatusClient
{
    static partial void UpdateJsonSerializerSettings(JsonSerializerOptions settings)
        => ApiClientJsonSettings.Configure(settings);
}
