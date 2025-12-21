using Microsoft.VisualStudio.TestTools.UnitTesting;
using PoTool.Client.Services;

namespace PoTool.Tests.Unit.Services;

[TestClass]
public class OnboardingServiceTests
{
    private OnboardingService _onboardingService = null!;
    private MockPreferencesService _mockPreferences = null!;

    [TestInitialize]
    public void Initialize()
    {
        _mockPreferences = new MockPreferencesService();
        _onboardingService = new OnboardingService(_mockPreferences);
    }

    [TestMethod]
    public void HasCompletedOnboarding_WhenNotCompleted_ReturnsFalse()
    {
        // Act
        var result = _onboardingService.HasCompletedOnboarding();

        // Assert
        Assert.IsFalse(result);
    }

    [TestMethod]
    public void HasCompletedOnboarding_WhenCompleted_ReturnsTrue()
    {
        // Arrange
        _onboardingService.MarkOnboardingCompleted();

        // Act
        var result = _onboardingService.HasCompletedOnboarding();

        // Assert
        Assert.IsTrue(result);
    }

    [TestMethod]
    public void HasCompletedOnboarding_WhenSkipped_ReturnsTrue()
    {
        // Arrange
        _onboardingService.MarkOnboardingSkipped();

        // Act
        var result = _onboardingService.HasCompletedOnboarding();

        // Assert
        Assert.IsTrue(result);
    }

    [TestMethod]
    public void MarkOnboardingCompleted_SetsCompletedFlag()
    {
        // Act
        _onboardingService.MarkOnboardingCompleted();

        // Assert
        Assert.IsTrue(_mockPreferences.GetBool("OnboardingCompleted", false));
        Assert.IsFalse(_mockPreferences.GetBool("OnboardingSkipped", false));
    }

    [TestMethod]
    public void MarkOnboardingSkipped_SetsSkippedFlag()
    {
        // Act
        _onboardingService.MarkOnboardingSkipped();

        // Assert
        Assert.IsTrue(_mockPreferences.GetBool("OnboardingSkipped", false));
        Assert.IsFalse(_mockPreferences.GetBool("OnboardingCompleted", false));
    }

    [TestMethod]
    public void ResetOnboarding_ClearsAllFlags()
    {
        // Arrange
        _onboardingService.MarkOnboardingCompleted();

        // Act
        _onboardingService.ResetOnboarding();

        // Assert
        Assert.IsFalse(_onboardingService.HasCompletedOnboarding());
    }

    [TestMethod]
    public void MarkOnboardingCompleted_AfterSkipped_ClearsSkippedFlag()
    {
        // Arrange
        _onboardingService.MarkOnboardingSkipped();

        // Act
        _onboardingService.MarkOnboardingCompleted();

        // Assert
        Assert.IsTrue(_onboardingService.HasCompletedOnboarding());
        Assert.IsTrue(_mockPreferences.GetBool("OnboardingCompleted", false));
        Assert.IsFalse(_mockPreferences.GetBool("OnboardingSkipped", false));
    }

    [TestMethod]
    public void MarkOnboardingSkipped_AfterCompleted_ClearsCompletedFlag()
    {
        // Arrange
        _onboardingService.MarkOnboardingCompleted();

        // Act
        _onboardingService.MarkOnboardingSkipped();

        // Assert
        Assert.IsTrue(_onboardingService.HasCompletedOnboarding());
        Assert.IsFalse(_mockPreferences.GetBool("OnboardingCompleted", false));
        Assert.IsTrue(_mockPreferences.GetBool("OnboardingSkipped", false));
    }

    /// <summary>
    /// Mock implementation of IPreferencesService for testing.
    /// </summary>
    private class MockPreferencesService : IPreferencesService
    {
        private readonly Dictionary<string, bool> _storage = new();

        public bool GetBool(string key, bool defaultValue)
        {
            return _storage.TryGetValue(key, out var value) ? value : defaultValue;
        }

        public void SetBool(string key, bool value)
        {
            _storage[key] = value;
        }

        public void Remove(string key)
        {
            _storage.Remove(key);
        }
    }
}
