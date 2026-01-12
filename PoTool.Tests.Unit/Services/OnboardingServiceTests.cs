using Microsoft.VisualStudio.TestTools.UnitTesting;
using PoTool.Client.Services;

using PoTool.Core.WorkItems;

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
    public async Task HasCompletedOnboarding_WhenNotCompleted_ReturnsFalse()
    {
        // Act
        var result = await _onboardingService.HasCompletedOnboardingAsync();

        // Assert
        Assert.IsFalse(result);
    }

    [TestMethod]
    public async Task HasCompletedOnboarding_WhenCompleted_ReturnsTrue()
    {
        // Arrange
        await _onboardingService.MarkOnboardingCompletedAsync();

        // Act
        var result = await _onboardingService.HasCompletedOnboardingAsync();

        // Assert
        Assert.IsTrue(result);
    }

    [TestMethod]
    public async Task HasCompletedOnboarding_WhenSkipped_ReturnsTrue()
    {
        // Arrange
        await _onboardingService.MarkOnboardingSkippedAsync();

        // Act
        var result = await _onboardingService.HasCompletedOnboardingAsync();

        // Assert
        Assert.IsTrue(result);
    }

    [TestMethod]
    public async Task MarkOnboardingCompleted_SetsCompletedFlag()
    {
        // Act
        await _onboardingService.MarkOnboardingCompletedAsync();

        // Assert
        Assert.IsTrue(await _mockPreferences.GetBoolAsync("OnboardingCompleted", false));
        Assert.IsFalse(await _mockPreferences.GetBoolAsync("OnboardingSkipped", false));
    }

    [TestMethod]
    public async Task MarkOnboardingSkipped_SetsSkippedFlag()
    {
        // Act
        await _onboardingService.MarkOnboardingSkippedAsync();

        // Assert
        Assert.IsTrue(await _mockPreferences.GetBoolAsync("OnboardingSkipped", false));
        Assert.IsFalse(await _mockPreferences.GetBoolAsync("OnboardingCompleted", false));
    }

    [TestMethod]
    public async Task ResetOnboarding_ClearsAllFlags()
    {
        // Arrange
        await _onboardingService.MarkOnboardingCompletedAsync();

        // Act
        await _onboardingService.ResetOnboardingAsync();

        // Assert
        Assert.IsFalse(await _onboardingService.HasCompletedOnboardingAsync());
    }

    [TestMethod]
    public async Task MarkOnboardingCompleted_AfterSkipped_ClearsSkippedFlag()
    {
        // Arrange
        await _onboardingService.MarkOnboardingSkippedAsync();

        // Act
        await _onboardingService.MarkOnboardingCompletedAsync();

        // Assert
        Assert.IsTrue(await _onboardingService.HasCompletedOnboardingAsync());
        Assert.IsTrue(await _mockPreferences.GetBoolAsync("OnboardingCompleted", false));
        Assert.IsFalse(await _mockPreferences.GetBoolAsync("OnboardingSkipped", false));
    }

    [TestMethod]
    public async Task MarkOnboardingSkipped_AfterCompleted_ClearsCompletedFlag()
    {
        // Arrange
        await _onboardingService.MarkOnboardingCompletedAsync();

        // Act
        await _onboardingService.MarkOnboardingSkippedAsync();

        // Assert
        Assert.IsTrue(await _onboardingService.HasCompletedOnboardingAsync());
        Assert.IsFalse(await _mockPreferences.GetBoolAsync("OnboardingCompleted", false));
        Assert.IsTrue(await _mockPreferences.GetBoolAsync("OnboardingSkipped", false));
    }

    /// <summary>
    /// Mock implementation of IPreferencesService for testing.
    /// </summary>
    private class MockPreferencesService : IPreferencesService
    {
        private readonly Dictionary<string, object?> _storage = new();

        public Task<bool> GetBoolAsync(string key, bool defaultValue)
        {
            return Task.FromResult(_storage.TryGetValue(key, out var value) && value is bool b ? b : defaultValue);
        }

        public Task SetBoolAsync(string key, bool value)
        {
            _storage[key] = value;
            return Task.CompletedTask;
        }

        public Task<int?> GetIntAsync(string key)
        {
            return Task.FromResult(_storage.TryGetValue(key, out var value) && value is int i ? (int?)i : null);
        }

        public Task SetIntAsync(string key, int value)
        {
            _storage[key] = value;
            return Task.CompletedTask;
        }

        public Task RemoveAsync(string key)
        {
            _storage.Remove(key);
            return Task.CompletedTask;
        }
    }
}
