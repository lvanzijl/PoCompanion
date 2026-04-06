using Microsoft.Extensions.Configuration;
using PoTool.Client.Models;
using PoTool.Client.Services;

namespace PoTool.Tests.Unit.Services;

[TestClass]
public class FeatureFlagServiceTests
{
    [TestMethod]
    public void IsEnabled_ReturnsConfiguredFlagValue()
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                [$"FeatureFlags:{FeatureFlagKeys.OnboardingWorkspace}"] = "true"
            })
            .Build();

        var service = new FeatureFlagService(configuration);

        Assert.IsTrue(service.IsEnabled(FeatureFlagKeys.OnboardingWorkspace));
    }

    [TestMethod]
    public void IsEnabled_ReturnsFalse_WhenFlagMissing()
    {
        var configuration = new ConfigurationBuilder().Build();
        var service = new FeatureFlagService(configuration);

        Assert.IsFalse(service.IsEnabled(FeatureFlagKeys.OnboardingWorkspace));
    }
}
