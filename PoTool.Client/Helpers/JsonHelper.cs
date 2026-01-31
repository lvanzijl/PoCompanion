using System.Text.Json;

namespace PoTool.Client.Helpers;

/// <summary>
/// Helper class for JSON serialization/deserialization options.
/// </summary>
public static class JsonHelper
{
    /// <summary>
    /// Standard JsonSerializerOptions with PropertyNameCaseInsensitive = true.
    /// Used for API responses from ASP.NET Core which use camelCase by default.
    /// </summary>
    public static readonly JsonSerializerOptions CaseInsensitiveOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };
}
