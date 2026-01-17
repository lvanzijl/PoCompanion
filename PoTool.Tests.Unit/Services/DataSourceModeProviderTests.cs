using Microsoft.Extensions.Configuration;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using PoTool.Api.Services;
using PoTool.Core.Configuration;

namespace PoTool.Tests.Unit.Services;

[TestClass]
public class DataSourceModeProviderTests
{
    [TestMethod]
    public void Constructor_WhenConfigurationMissing_DefaultsToLive()
    {
        // Arrange
        var configuration = new ConfigurationBuilder().Build();

        // Act
        var provider = new DataSourceModeProvider(configuration);

        // Assert
        Assert.AreEqual(DataSourceMode.Live, provider.Mode);
    }

    [TestMethod]
    public void Constructor_WhenConfigurationEmpty_DefaultsToLive()
    {
        // Arrange
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                { "DataSourceMode", "" }
            })
            .Build();

        // Act
        var provider = new DataSourceModeProvider(configuration);

        // Assert
        Assert.AreEqual(DataSourceMode.Live, provider.Mode);
    }

    [TestMethod]
    public void Constructor_WhenConfigurationWhitespace_DefaultsToLive()
    {
        // Arrange
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                { "DataSourceMode", "   " }
            })
            .Build();

        // Act
        var provider = new DataSourceModeProvider(configuration);

        // Assert
        Assert.AreEqual(DataSourceMode.Live, provider.Mode);
    }

    [TestMethod]
    public void Constructor_WhenConfigurationInvalid_DefaultsToLive()
    {
        // Arrange
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                { "DataSourceMode", "InvalidMode" }
            })
            .Build();

        // Act
        var provider = new DataSourceModeProvider(configuration);

        // Assert
        Assert.AreEqual(DataSourceMode.Live, provider.Mode);
    }

    [TestMethod]
    public void Constructor_WhenConfigurationIsLive_ReturnsLive()
    {
        // Arrange
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                { "DataSourceMode", "Live" }
            })
            .Build();

        // Act
        var provider = new DataSourceModeProvider(configuration);

        // Assert
        Assert.AreEqual(DataSourceMode.Live, provider.Mode);
    }

    [TestMethod]
    public void Constructor_WhenConfigurationIsLiveLowercase_ReturnsLive()
    {
        // Arrange
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                { "DataSourceMode", "live" }
            })
            .Build();

        // Act
        var provider = new DataSourceModeProvider(configuration);

        // Assert
        Assert.AreEqual(DataSourceMode.Live, provider.Mode);
    }

    [TestMethod]
    public void Constructor_WhenConfigurationIsCached_ReturnsCached()
    {
        // Arrange
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                { "DataSourceMode", "Cached" }
            })
            .Build();

        // Act
        var provider = new DataSourceModeProvider(configuration);

        // Assert
#pragma warning disable CS0618 // Type or member is obsolete - Testing backward compatibility
        Assert.AreEqual(DataSourceMode.Cached, provider.Mode);
#pragma warning restore CS0618
    }

    [TestMethod]
    public void Constructor_WhenConfigurationIsCachedLowercase_ReturnsCached()
    {
        // Arrange
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                { "DataSourceMode", "cached" }
            })
            .Build();

        // Act
        var provider = new DataSourceModeProvider(configuration);

        // Assert
#pragma warning disable CS0618 // Type or member is obsolete - Testing backward compatibility
        Assert.AreEqual(DataSourceMode.Cached, provider.Mode);
#pragma warning restore CS0618
    }

    [TestMethod]
    public void Constructor_WhenConfigurationIsCachedMixedCase_ReturnsCached()
    {
        // Arrange
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                { "DataSourceMode", "CaChEd" }
            })
            .Build();

        // Act
        var provider = new DataSourceModeProvider(configuration);

        // Assert
#pragma warning disable CS0618 // Type or member is obsolete - Testing backward compatibility
        Assert.AreEqual(DataSourceMode.Cached, provider.Mode);
#pragma warning restore CS0618
    }
}
