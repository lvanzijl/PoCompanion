using Microsoft.VisualStudio.TestTools.UnitTesting;
using PoTool.Client.Services;
using StartupReadinessDto = PoTool.Client.Services.StartupReadinessDto;

using PoTool.Core.WorkItems;

namespace PoTool.Tests.Unit.Services;

/// <summary>
/// Unit tests for the StartupOrchestratorService decision tree.
/// Tests the startup routing logic as specified in User_landing_v2.md.
/// </summary>
[TestClass]
public class StartupOrchestratorServiceTests
{
    private StartupOrchestratorService CreateService()
    {
        // For unit tests, we don't need a real HttpClient since we're testing the DetermineRoute method
        // which doesn't make HTTP calls
        return new StartupOrchestratorService(new HttpClient());
    }

    #region DetermineRoute Tests - Mock Mode

    [TestMethod]
    public void DetermineRoute_MockModeEnabled_ReturnsProfilesHomeWithUsableApp()
    {
        // Arrange
        var service = CreateService();
        var readiness = new StartupReadinessDto(
            IsMockDataEnabled: true,
            HasSavedTfsConfig: false,
            HasTestedConnectionSuccessfully: false,
            HasVerifiedTfsApiSuccessfully: false,
            HasAnyProfile: false,
            ActiveProfileId: null,
            MissingRequirementMessage: null
        );

        // Act
        var result = service.DetermineRoute(readiness);

        // Assert
        Assert.AreEqual(StartupRoute.ProfilesHome, result.Route);
        Assert.IsTrue(result.IsAppUsable);
        Assert.IsNull(result.Message);
    }

    [TestMethod]
    public void DetermineRoute_MockModeWithNoProfiles_StillUsable()
    {
        // Arrange
        var service = CreateService();
        var readiness = new StartupReadinessDto(
            IsMockDataEnabled: true,
            HasSavedTfsConfig: false,
            HasTestedConnectionSuccessfully: false,
            HasVerifiedTfsApiSuccessfully: false,
            HasAnyProfile: false,
            ActiveProfileId: null,
            MissingRequirementMessage: null
        );

        // Act
        var result = service.DetermineRoute(readiness);

        // Assert - In mock mode, app should be usable even without profiles
        Assert.IsTrue(result.IsAppUsable);
    }

    #endregion

    #region DetermineRoute Tests - Real TFS Mode

    [TestMethod]
    public void DetermineRoute_NoTfsConfig_ReturnsConfigurationRoute()
    {
        // Arrange
        var service = CreateService();
        var readiness = new StartupReadinessDto(
            IsMockDataEnabled: false,
            HasSavedTfsConfig: false,
            HasTestedConnectionSuccessfully: false,
            HasVerifiedTfsApiSuccessfully: false,
            HasAnyProfile: false,
            ActiveProfileId: null,
            MissingRequirementMessage: "Configuration required"
        );

        // Act
        var result = service.DetermineRoute(readiness);

        // Assert
        Assert.AreEqual(StartupRoute.Configuration, result.Route);
        Assert.IsFalse(result.IsAppUsable);
        Assert.IsNotNull(result.Message);
        Assert.Contains("Configuration", result.Message);
    }

    [TestMethod]
    public void DetermineRoute_ConfigSavedButNotTested_ReturnsConfigurationRoute()
    {
        // Arrange
        var service = CreateService();
        var readiness = new StartupReadinessDto(
            IsMockDataEnabled: false,
            HasSavedTfsConfig: true,
            HasTestedConnectionSuccessfully: false,
            HasVerifiedTfsApiSuccessfully: false,
            HasAnyProfile: false,
            ActiveProfileId: null,
            MissingRequirementMessage: "Test Connection required"
        );

        // Act
        var result = service.DetermineRoute(readiness);

        // Assert
        Assert.AreEqual(StartupRoute.Configuration, result.Route);
        Assert.IsFalse(result.IsAppUsable);
        Assert.Contains("Test Connection", result.Message!);
    }

    [TestMethod]
    public void DetermineRoute_TestedButNotVerified_ReturnsConfigurationRoute()
    {
        // Arrange
        var service = CreateService();
        var readiness = new StartupReadinessDto(
            IsMockDataEnabled: false,
            HasSavedTfsConfig: true,
            HasTestedConnectionSuccessfully: true,
            HasVerifiedTfsApiSuccessfully: false,
            HasAnyProfile: false,
            ActiveProfileId: null,
            MissingRequirementMessage: "Verify TFS API required"
        );

        // Act
        var result = service.DetermineRoute(readiness);

        // Assert
        Assert.AreEqual(StartupRoute.Configuration, result.Route);
        Assert.IsFalse(result.IsAppUsable);
        Assert.Contains("Verify", result.Message!);
    }

    [TestMethod]
    public void DetermineRoute_VerifiedButNoProfile_ReturnsCreateFirstProfileRoute()
    {
        // Arrange
        var service = CreateService();
        var readiness = new StartupReadinessDto(
            IsMockDataEnabled: false,
            HasSavedTfsConfig: true,
            HasTestedConnectionSuccessfully: true,
            HasVerifiedTfsApiSuccessfully: true,
            HasAnyProfile: false,
            ActiveProfileId: null,
            MissingRequirementMessage: "Profile required"
        );

        // Act
        var result = service.DetermineRoute(readiness);

        // Assert
        Assert.AreEqual(StartupRoute.CreateFirstProfile, result.Route);
        Assert.IsFalse(result.IsAppUsable);
        Assert.Contains("Profile", result.Message!);
    }

    [TestMethod]
    public void DetermineRoute_HasProfileButNoneActive_ReturnsProfilesHomeNotUsable()
    {
        // Arrange
        var service = CreateService();
        var readiness = new StartupReadinessDto(
            IsMockDataEnabled: false,
            HasSavedTfsConfig: true,
            HasTestedConnectionSuccessfully: true,
            HasVerifiedTfsApiSuccessfully: true,
            HasAnyProfile: true,
            ActiveProfileId: null,
            MissingRequirementMessage: "Profile selection required"
        );

        // Act
        var result = service.DetermineRoute(readiness);

        // Assert
        Assert.AreEqual(StartupRoute.ProfilesHome, result.Route);
        Assert.IsFalse(result.IsAppUsable);
        Assert.Contains("select", result.Message!);
    }

    [TestMethod]
    public void DetermineRoute_AllRequirementsMet_ReturnsProfilesHomeUsable()
    {
        // Arrange
        var service = CreateService();
        var readiness = new StartupReadinessDto(
            IsMockDataEnabled: false,
            HasSavedTfsConfig: true,
            HasTestedConnectionSuccessfully: true,
            HasVerifiedTfsApiSuccessfully: true,
            HasAnyProfile: true,
            ActiveProfileId: 1,
            MissingRequirementMessage: null
        );

        // Act
        var result = service.DetermineRoute(readiness);

        // Assert
        Assert.AreEqual(StartupRoute.ProfilesHome, result.Route);
        Assert.IsTrue(result.IsAppUsable);
        Assert.IsNull(result.Message);
    }

    #endregion

    #region IsFeaturePageAccessible Tests

    [TestMethod]
    public void IsFeaturePageAccessible_MockMode_ReturnsTrue()
    {
        // Arrange
        var service = CreateService();
        var readiness = new StartupReadinessDto(
            IsMockDataEnabled: true,
            HasSavedTfsConfig: false,
            HasTestedConnectionSuccessfully: false,
            HasVerifiedTfsApiSuccessfully: false,
            HasAnyProfile: false,
            ActiveProfileId: null,
            MissingRequirementMessage: null
        );

        // Act
        var result = service.IsFeaturePageAccessible(readiness);

        // Assert
        Assert.IsTrue(result);
    }

    [TestMethod]
    public void IsFeaturePageAccessible_RealModeNotVerified_ReturnsFalse()
    {
        // Arrange
        var service = CreateService();
        var readiness = new StartupReadinessDto(
            IsMockDataEnabled: false,
            HasSavedTfsConfig: true,
            HasTestedConnectionSuccessfully: true,
            HasVerifiedTfsApiSuccessfully: false,
            HasAnyProfile: true,
            ActiveProfileId: 1,
            MissingRequirementMessage: null
        );

        // Act
        var result = service.IsFeaturePageAccessible(readiness);

        // Assert
        Assert.IsFalse(result);
    }

    [TestMethod]
    public void IsFeaturePageAccessible_RealModeNoProfile_ReturnsFalse()
    {
        // Arrange
        var service = CreateService();
        var readiness = new StartupReadinessDto(
            IsMockDataEnabled: false,
            HasSavedTfsConfig: true,
            HasTestedConnectionSuccessfully: true,
            HasVerifiedTfsApiSuccessfully: true,
            HasAnyProfile: false,
            ActiveProfileId: null,
            MissingRequirementMessage: null
        );

        // Act
        var result = service.IsFeaturePageAccessible(readiness);

        // Assert
        Assert.IsFalse(result);
    }

    [TestMethod]
    public void IsFeaturePageAccessible_RealModeNoActiveProfile_ReturnsFalse()
    {
        // Arrange
        var service = CreateService();
        var readiness = new StartupReadinessDto(
            IsMockDataEnabled: false,
            HasSavedTfsConfig: true,
            HasTestedConnectionSuccessfully: true,
            HasVerifiedTfsApiSuccessfully: true,
            HasAnyProfile: true,
            ActiveProfileId: null,
            MissingRequirementMessage: null
        );

        // Act
        var result = service.IsFeaturePageAccessible(readiness);

        // Assert
        Assert.IsFalse(result);
    }

    [TestMethod]
    public void IsFeaturePageAccessible_RealModeAllRequirementsMet_ReturnsTrue()
    {
        // Arrange
        var service = CreateService();
        var readiness = new StartupReadinessDto(
            IsMockDataEnabled: false,
            HasSavedTfsConfig: true,
            HasTestedConnectionSuccessfully: true,
            HasVerifiedTfsApiSuccessfully: true,
            HasAnyProfile: true,
            ActiveProfileId: 1,
            MissingRequirementMessage: null
        );

        // Act
        var result = service.IsFeaturePageAccessible(readiness);

        // Assert
        Assert.IsTrue(result);
    }

    #endregion
}
