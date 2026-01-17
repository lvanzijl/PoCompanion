using Microsoft.Extensions.Configuration;
using PoTool.Core.Configuration;

namespace PoTool.Api.Services;

/// <summary>
/// Provides the active data source mode by reading from configuration.
/// Default is Live mode if configuration is missing or invalid.
/// </summary>
public sealed class DataSourceModeProvider : IDataSourceModeProvider
{
    public DataSourceMode Mode { get; }

    public DataSourceModeProvider(IConfiguration configuration)
    {
        // Read from configuration with default to Live
        var configValue = configuration.GetValue<string?>("DataSourceMode", null);
        
        // Parse the configuration value
        if (string.IsNullOrWhiteSpace(configValue))
        {
            // Missing configuration -> default to Live
            Mode = DataSourceMode.Live;
        }
        else if (Enum.TryParse<DataSourceMode>(configValue, ignoreCase: true, out var parsedMode))
        {
            // Valid configuration value
            Mode = parsedMode;
        }
        else
        {
            // Invalid configuration value -> default to Live
            Mode = DataSourceMode.Live;
        }
    }
}
