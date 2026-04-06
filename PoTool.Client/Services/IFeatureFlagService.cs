namespace PoTool.Client.Services;

public interface IFeatureFlagService
{
    bool IsEnabled(string key);
}
