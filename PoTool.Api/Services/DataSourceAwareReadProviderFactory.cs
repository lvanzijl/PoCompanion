using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using PoTool.Api.Exceptions;
using PoTool.Core.Configuration;
using PoTool.Core.Contracts;
using Microsoft.AspNetCore.Http;

namespace PoTool.Api.Services;

/// <summary>
/// Factory for resolving read providers that still switch between Live and Cache at runtime.
/// Pull request and pipeline analytical reads are now bound directly to cached providers.
/// </summary>
public sealed class DataSourceAwareReadProviderFactory
{
    private enum WorkItemProviderKind
    {
        Cache,
        Live
    }

    private readonly IServiceProvider _serviceProvider;
    private readonly IDataSourceModeProvider _modeProvider;
    private readonly ILogger<DataSourceAwareReadProviderFactory> _logger;
    private readonly IHttpContextAccessor? _httpContextAccessor;

    public DataSourceAwareReadProviderFactory(
        IServiceProvider serviceProvider,
        IDataSourceModeProvider modeProvider,
        ILogger<DataSourceAwareReadProviderFactory> logger)
        : this(serviceProvider, modeProvider, logger, null)
    {
    }

    public DataSourceAwareReadProviderFactory(
        IServiceProvider serviceProvider,
        IDataSourceModeProvider modeProvider,
        ILogger<DataSourceAwareReadProviderFactory> logger,
        IHttpContextAccessor? httpContextAccessor)
    {
        _serviceProvider = serviceProvider;
        _modeProvider = modeProvider;
        _logger = logger;
        _httpContextAccessor = httpContextAccessor;
    }

    /// <summary>
    /// Gets the work item read provider based on current data source mode.
    /// </summary>
    public IWorkItemReadProvider GetWorkItemReadProvider()
    {
        var mode = _modeProvider.Mode;
        var route = _httpContextAccessor?.HttpContext?.Request.Path.Value ?? "<unknown>";

        return mode switch
        {
            DataSourceMode.Cache => ResolveProvider(route, "CacheOnly", WorkItemProviderKind.Cache),
            DataSourceMode.Live => ResolveProvider(route, "LiveAllowed", WorkItemProviderKind.Live),
            _ => throw new InvalidDataSourceUsageException(route, mode.ToString(), "Unknown")
        };
    }

    private IWorkItemReadProvider ResolveProvider(string route, string modeName, WorkItemProviderKind providerKind)
    {
        if (_modeProvider.Mode == DataSourceMode.Cache && providerKind == WorkItemProviderKind.Live)
        {
            _logger.LogError(
                "[Violation] Route={Route} Mode={Mode} AttemptedProvider={AttemptedProvider} Action=Blocked",
                route,
                modeName,
                providerKind);
            throw new InvalidDataSourceUsageException(route, modeName, providerKind.ToString());
        }

        _logger.LogInformation(
            "[DataSourceMode] Route={Route} Mode={Mode} Provider={Provider}",
            route,
            modeName,
            providerKind);

        return providerKind == WorkItemProviderKind.Cache
            ? _serviceProvider.GetRequiredKeyedService<IWorkItemReadProvider>("Cached")
            : _serviceProvider.GetRequiredKeyedService<IWorkItemReadProvider>("Live");
    }

}
