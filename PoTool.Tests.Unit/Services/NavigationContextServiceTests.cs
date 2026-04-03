using Microsoft.AspNetCore.Components;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using PoTool.Client.Models;
using PoTool.Client.Services;
using ProfileDto = PoTool.Shared.Settings.ProfileDto;
using SettingsDto = PoTool.Shared.Settings.SettingsDto;
using ProfilePictureType = PoTool.Shared.Settings.ProfilePictureType;

namespace PoTool.Tests.Unit.Services;

[TestClass]
public class NavigationContextServiceTests
{
    private NavigationContextService _service = null!;
    private MockNavigationManager _navigationManager = null!;
    private MockProfileService _profileService = null!;

    [TestInitialize]
    public void Initialize()
    {
        _navigationManager = new MockNavigationManager();
        _profileService = new MockProfileService();
        _service = new NavigationContextService(_navigationManager, _profileService);
    }

    [TestMethod]
    public void CurrentOrDefault_WhenNoContextSet_ReturnsNull()
    {
        // Act
        var result = _service.CurrentOrDefault;

        // Assert
        Assert.IsNull(result);
    }

    [TestMethod]
    public void SetInitialContext_CreatesValidContext()
    {
        // Arrange
        _profileService.SetCachedActiveProfileId(1);

        // Act
        _service.SetInitialContext(Intent.Overzien, 1);
        var result = _service.CurrentOrDefault;

        // Assert
        Assert.IsNotNull(result);
        Assert.AreEqual(Intent.Overzien, result.Intent);
        Assert.AreEqual(ScopeLevel.Portfolio, result.Scope.Level);
        Assert.AreEqual(1, result.Scope.ProfileId);
        Assert.AreEqual(TriggerType.Choice, result.Trigger?.Type);
        Assert.AreEqual(TimeHorizon.Current, result.TimeHorizon);
    }

    [TestMethod]
    public void SetInitialContext_WithPlannen_SetsDefaultFutureTimeHorizon()
    {
        // Arrange
        _profileService.SetCachedActiveProfileId(1);

        // Act
        _service.SetInitialContext(Intent.Plannen, 1);
        var result = _service.CurrentOrDefault;

        // Assert
        Assert.IsNotNull(result);
        Assert.AreEqual(Intent.Plannen, result.Intent);
        Assert.AreEqual(TimeHorizon.Future, result.TimeHorizon);
    }

    [TestMethod]
    public void HasValidProfile_WhenNoContext_ReturnsFalse()
    {
        // Act
        var result = _service.HasValidProfile();

        // Assert
        Assert.IsFalse(result);
    }

    [TestMethod]
    public void HasValidProfile_WhenProfileSet_ReturnsTrue()
    {
        // Arrange
        _profileService.SetCachedActiveProfileId(1);
        _service.SetInitialContext(Intent.Overzien, 1);

        // Act
        var result = _service.HasValidProfile();

        // Assert
        Assert.IsTrue(result);
    }

    [TestMethod]
    public void Current_WhenNoProfile_ThrowsException()
    {
        // Act & Assert
        try
        {
            _ = _service.Current;
            Assert.Fail("Expected InvalidOperationException was not thrown");
        }
        catch (InvalidOperationException)
        {
            // Expected exception
        }
    }

    [TestMethod]
    public void WithUpdates_CreatesNewImmutableContext()
    {
        // Arrange
        _profileService.SetCachedActiveProfileId(1);
        _service.SetInitialContext(Intent.Overzien, 1);
        var originalContext = _service.CurrentOrDefault;

        // Act
        var newContext = _service.WithUpdates(ctx => ctx with
        {
            TimeHorizon = TimeHorizon.Historical
        });

        // Assert
        Assert.AreNotSame(originalContext, newContext);
        Assert.AreEqual(TimeHorizon.Current, originalContext!.TimeHorizon);
        Assert.AreEqual(TimeHorizon.Historical, newContext.TimeHorizon);
    }

    [TestMethod]
    public async Task NavigateWithContextAsync_UpdatesCurrentContext()
    {
        // Arrange
        _profileService.SetCachedActiveProfileId(1);
        _service.SetInitialContext(Intent.Overzien, 1);

        var newContext = new NavigationContext
        {
            Intent = Intent.Plannen,
            Scope = new Scope { Level = ScopeLevel.Product, ProfileId = 1, ProductId = 42 },
            Trigger = new Trigger { Type = TriggerType.Deviation },
            TimeHorizon = TimeHorizon.Future
        };

        // Act
        await _service.NavigateWithContextAsync("/home/planning", newContext);

        // Assert
        Assert.AreEqual(Intent.Plannen, _service.CurrentOrDefault?.Intent);
        Assert.IsNotNull(_service.CurrentOrDefault?.Parent);
    }

    [TestMethod]
    public async Task NavigateWithContextAsync_NavigatesToUrlWithQueryString()
    {
        // Arrange
        _profileService.SetCachedActiveProfileId(1);
        _service.SetInitialContext(Intent.Overzien, 1);

        var newContext = new NavigationContext
        {
            Intent = Intent.Overzien,
            Scope = new Scope { Level = ScopeLevel.Product, ProfileId = 1, ProjectAlias = "payments-platform", ProductId = 42 },
            Trigger = new Trigger { Type = TriggerType.Choice },
            TimeHorizon = TimeHorizon.Current
        };

        // Act
        await _service.NavigateWithContextAsync("/workspace/product", newContext);

        // Assert
        Assert.IsNotNull(_navigationManager.LastNavigatedUri);
        StringAssert.StartsWith(_navigationManager.LastNavigatedUri, "/workspace/product?");
        StringAssert.Contains(_navigationManager.LastNavigatedUri, "intent=overzien");
        StringAssert.Contains(_navigationManager.LastNavigatedUri, "projectAlias=payments-platform");
        StringAssert.Contains(_navigationManager.LastNavigatedUri, "productId=42");
    }

    [TestMethod]
    public void ToQueryString_SerializesContextCorrectly()
    {
        // Arrange
        var context = new NavigationContext
        {
            Intent = Intent.Overzien,
            Scope = new Scope { Level = ScopeLevel.Product, ProfileId = 1, ProjectAlias = "payments-platform", ProductId = 42 },
            Trigger = new Trigger { Type = TriggerType.Deviation },
            TimeHorizon = TimeHorizon.Historical
        };

        // Act
        var queryString = _service.ToQueryString(context);

        // Assert
        StringAssert.Contains(queryString, "intent=overzien");
        StringAssert.Contains(queryString, "scope=product");
        StringAssert.Contains(queryString, "projectAlias=payments-platform");
        StringAssert.Contains(queryString, "productId=42");
        StringAssert.Contains(queryString, "time=historical");
        StringAssert.Contains(queryString, "trigger=deviation");
    }

    [TestMethod]
    public void FromQueryString_ParsesContextCorrectly()
    {
        // Arrange
        _profileService.SetCachedActiveProfileId(1);
        var queryString = "intent=overzien&scope=product&projectAlias=payments-platform&productId=42&time=historical&trigger=deviation";

        // Act
        var context = _service.FromQueryString(queryString);

        // Assert
        Assert.IsNotNull(context);
        Assert.AreEqual(Intent.Overzien, context.Intent);
        Assert.AreEqual(ScopeLevel.Product, context.Scope.Level);
        Assert.AreEqual("payments-platform", context.Scope.ProjectAlias);
        Assert.AreEqual(42, context.Scope.ProductId);
        Assert.AreEqual(TimeHorizon.Historical, context.TimeHorizon);
        Assert.AreEqual(TriggerType.Deviation, context.Trigger?.Type);
    }

    [TestMethod]
    public void FromQueryString_WhenEmpty_ReturnsNull()
    {
        // Act
        var result = _service.FromQueryString("");

        // Assert
        Assert.IsNull(result);
    }

    [TestMethod]
    public void FromQueryString_WhenMissingIntent_ReturnsNull()
    {
        // Arrange
        var queryString = "scope=product&productId=42";

        // Act
        var result = _service.FromQueryString(queryString);

        // Assert
        Assert.IsNull(result);
    }

    [TestMethod]
    public void ContextChanged_EventRaisedOnSetInitialContext()
    {
        // Arrange
        _profileService.SetCachedActiveProfileId(1);
        NavigationContextChangedEventArgs? eventArgs = null;
        _service.ContextChanged += (_, args) => eventArgs = args;

        // Act
        _service.SetInitialContext(Intent.Overzien, 1);

        // Assert
        Assert.IsNotNull(eventArgs);
        Assert.IsNull(eventArgs.Previous);
        Assert.IsNotNull(eventArgs.Current);
        Assert.AreEqual(Intent.Overzien, eventArgs.Current.Intent);
    }

    [TestMethod]
    public void ClearContext_ClearsCurrentContext()
    {
        // Arrange
        _profileService.SetCachedActiveProfileId(1);
        _service.SetInitialContext(Intent.Overzien, 1);

        // Act
        _service.ClearContext();

        // Assert
        Assert.IsNull(_service.CurrentOrDefault);
    }

    [TestMethod]
    public void CanPerform_NavigateToPlanning_ReturnsTrueWhenContextExists()
    {
        // Arrange
        _profileService.SetCachedActiveProfileId(1);
        _service.SetInitialContext(Intent.Overzien, 1);

        // Act
        var canNavigate = _service.CanPerform("navigate-to-planning");

        // Assert
        Assert.IsTrue(canNavigate);
    }

    /// <summary>
    /// Mock NavigationManager for testing.
    /// </summary>
    private class MockNavigationManager : NavigationManager
    {
        public string? LastNavigatedUri { get; private set; }

        public MockNavigationManager()
        {
            Initialize("https://localhost/", "https://localhost/");
        }

        protected override void NavigateToCore(string uri, bool forceLoad)
        {
            LastNavigatedUri = uri;
        }
    }

    /// <summary>
    /// Mock ProfileService for testing.
    /// </summary>
    private class MockProfileService : IProfileService
    {
        private int? _cachedProfileId;

        public int? GetActiveProfileId() => _cachedProfileId;

        public bool IsActiveProfileValid() => _cachedProfileId.HasValue;

        public void SetCachedActiveProfileId(int? profileId) => _cachedProfileId = profileId;

        public Task<IEnumerable<ProfileDto>> GetAllProfilesAsync(CancellationToken cancellationToken = default)
            => Task.FromResult<IEnumerable<ProfileDto>>(Array.Empty<ProfileDto>());

        public Task<ProfileDto?> GetProfileByIdAsync(int id, CancellationToken cancellationToken = default)
            => Task.FromResult<ProfileDto?>(null);

        public Task<ProfileDto?> GetActiveProfileAsync(CancellationToken cancellationToken = default)
            => Task.FromResult<ProfileDto?>(null);

        public Task<ProfileDto> CreateProfileAsync(string name, List<int> goalIds, ProfilePictureType pictureType = ProfilePictureType.Default, int? defaultPictureId = null, string? customPicturePath = null, CancellationToken cancellationToken = default)
            => Task.FromResult(new ProfileDto { Id = 1, Name = name });

        public Task<ProfileDto> UpdateProfileAsync(int id, string name, List<int> goalIds, ProfilePictureType? pictureType = null, int? defaultPictureId = null, string? customPicturePath = null, CancellationToken cancellationToken = default)
            => Task.FromResult(new ProfileDto { Id = id, Name = name });

        public Task<bool> DeleteProfileAsync(int id, CancellationToken cancellationToken = default)
            => Task.FromResult(true);

        public Task<SettingsDto> SetActiveProfileAsync(int? profileId, CancellationToken cancellationToken = default)
            => Task.FromResult(new SettingsDto());

        public Task<ProfileDto> CreateAndActivateProfileAsync(string name, List<int> goalIds, ProfilePictureType pictureType = ProfilePictureType.Default, int? defaultPictureId = null, string? customPicturePath = null, CancellationToken cancellationToken = default)
            => Task.FromResult(new ProfileDto { Id = 1, Name = name });
    }
}
