using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using PoTool.Core.Configuration;
using PoTool.Core.Contracts;

namespace PoTool.Api.Services;

/// <summary>
/// Factory for resolving read providers that still switch between Live and Cache at runtime.
/// Pull request analytical reads are now bound directly to the cached provider.
/// </summary>
public sealed class DataSourceAwareReadProviderFactory
{
    private readonly IServiceProvider _serviceProvider;
    private readonly IDataSourceModeProvider _modeProvider;
    private readonly ILogger<DataSourceAwareReadProviderFactory> _logger;

    public DataSourceAwareReadProviderFactory(
        IServiceProvider serviceProvider,
        IDataSourceModeProvider modeProvider,
        ILogger<DataSourceAwareReadProviderFactory> logger)
    {
        _serviceProvider = serviceProvider;
        _modeProvider = modeProvider;
        _logger = logger;
    }

    /// <summary>
    /// Gets the work item read provider based on current data source mode.
    /// </summary>
    public IWorkItemReadProvider GetWorkItemReadProvider()
    {
        var mode = _modeProvider.Mode;
        _logger.LogDebug("Resolving IWorkItemReadProvider for mode: {Mode}", mode);

        return mode switch
        {
            DataSourceMode.Cache => _serviceProvider.GetRequiredKeyedService<IWorkItemReadProvider>("Cached"),
            _ => _serviceProvider.GetRequiredKeyedService<IWorkItemReadProvider>("Live")
        };
    }

    /// <summary>
    /// Gets the pipeline read provider based on current data source mode.
    /// </summary>
    public IPipelineReadProvider GetPipelineReadProvider()
    {
        var mode = _modeProvider.Mode;
        _logger.LogDebug("Resolving IPipelineReadProvider for mode: {Mode}", mode);

        return mode switch
        {
            DataSourceMode.Cache => _serviceProvider.GetRequiredKeyedService<IPipelineReadProvider>("Cached"),
            _ => _serviceProvider.GetRequiredKeyedService<IPipelineReadProvider>("Live")
        };
    }
}
