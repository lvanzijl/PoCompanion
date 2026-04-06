using Microsoft.Extensions.Configuration;
using PoTool.Client.Models;

namespace PoTool.Client.Services;

public sealed class FeatureFlagService : IFeatureFlagService
{
    private readonly IConfiguration _configuration;

    public FeatureFlagService(IConfiguration configuration)
    {
        _configuration = configuration;
    }

    public bool IsEnabled(string key)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(key);
        return _configuration.GetValue<bool>($"FeatureFlags:{key}");
    }
}
