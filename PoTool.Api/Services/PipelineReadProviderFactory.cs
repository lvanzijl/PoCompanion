using Microsoft.Extensions.DependencyInjection;
using PoTool.Core.Configuration;
using PoTool.Core.Contracts;

namespace PoTool.Api.Services;

/// <summary>
/// Factory that creates the appropriate pipeline read provider based on the configured DataSourceMode.
/// This ensures handlers get the correct provider without needing to know about mode selection.
/// </summary>
public class PipelineReadProviderFactory
{
    private readonly IDataSourceModeProvider _modeProvider;
    private readonly IServiceProvider _serviceProvider;

    public PipelineReadProviderFactory(
        IDataSourceModeProvider modeProvider,
        IServiceProvider serviceProvider)
    {
        _modeProvider = modeProvider;
        _serviceProvider = serviceProvider;
    }

    /// <summary>
    /// Creates the appropriate pipeline read provider based on the current data source mode.
    /// </summary>
#pragma warning disable CS0618 // Type or member is obsolete - Cached mode is intentionally supported for backward compatibility
    public virtual IPipelineReadProvider Create()
    {
        return _modeProvider.Mode switch
        {
            DataSourceMode.Live => _serviceProvider.GetRequiredService<LivePipelineReadProvider>(),
            DataSourceMode.Cached => _serviceProvider.GetRequiredService<CachedPipelineReadProvider>(),
            _ => throw new InvalidOperationException($"Unsupported DataSourceMode: {_modeProvider.Mode}")
        };
    }
#pragma warning restore CS0618
}
