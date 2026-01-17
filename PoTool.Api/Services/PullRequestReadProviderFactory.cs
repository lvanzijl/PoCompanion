using Microsoft.Extensions.DependencyInjection;
using PoTool.Core.Configuration;
using PoTool.Core.Contracts;

namespace PoTool.Api.Services;

/// <summary>
/// Factory that creates the appropriate pull request read provider based on the configured DataSourceMode.
/// This ensures handlers get the correct provider without needing to know about mode selection.
/// </summary>
public class PullRequestReadProviderFactory
{
    private readonly IDataSourceModeProvider _modeProvider;
    private readonly IServiceProvider _serviceProvider;

    public PullRequestReadProviderFactory(
        IDataSourceModeProvider modeProvider,
        IServiceProvider serviceProvider)
    {
        _modeProvider = modeProvider;
        _serviceProvider = serviceProvider;
    }

    /// <summary>
    /// Creates the appropriate pull request read provider based on the current data source mode.
    /// </summary>
    public virtual IPullRequestReadProvider Create()
    {
        return _modeProvider.Mode switch
        {
            DataSourceMode.Live => _serviceProvider.GetRequiredService<LivePullRequestReadProvider>(),
            DataSourceMode.Cached => _serviceProvider.GetRequiredService<CachedPullRequestReadProvider>(),
            _ => throw new InvalidOperationException($"Unsupported DataSourceMode: {_modeProvider.Mode}")
        };
    }
}
