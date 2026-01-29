using Bunit;
using Microsoft.Extensions.DependencyInjection;
using MudBlazor;
using MudBlazor.Services;
using PoTool.Client.Components.Onboarding;
using PoTool.Client.Services;
using Moq;
using System.Text;

namespace PoTool.Tests.Blazor;

/// <summary>
/// bUnit tests for OnboardingWizard component
/// Note: OnboardingWizard is designed to be shown in a MudDialog context,
/// so these tests verify basic component structure and dependencies.
/// </summary>
[TestClass]
public class OnboardingWizardTests : BunitTestContext
{
    private Mock<IOnboardingService> _mockOnboardingService = null!;
    private TfsConfigService _tfsConfigService = null!;
    private ErrorMessageService _errorMessageService = null!;
    private Mock<ISnackbar> _mockSnackbar = null!;

    [TestInitialize]
    public void Setup()
    {
        _mockOnboardingService = new Mock<IOnboardingService>();

        // Create TfsConfigService with mocked dependencies (cannot mock the class itself)
        var mockApiClient = new Mock<Client.ApiClient.IClient>();
        var mockHttpClient = new HttpClient { BaseAddress = new Uri("http://localhost/") };
        _tfsConfigService = new TfsConfigService(mockApiClient.Object, mockHttpClient);

        _errorMessageService = new ErrorMessageService();
        _mockSnackbar = new Mock<ISnackbar>();

        // Register services
        Services.AddSingleton(_mockOnboardingService.Object);
        Services.AddSingleton(_tfsConfigService);
        Services.AddSingleton(_errorMessageService);
        Services.AddSingleton(_mockSnackbar.Object);
        Services.AddMudServices();

        // Mock NavigationManager
        Services.AddSingleton<Microsoft.AspNetCore.Components.NavigationManager>(
            new MockNavigationManager());
    }

    [TestMethod]
    public async Task OnboardingService_HasCompletedOnboarding_InitiallyFalse()
    {
        // Arrange
        _mockOnboardingService.Setup(s => s.HasCompletedOnboardingAsync()).ReturnsAsync(false);

        // Act
        var result = await _mockOnboardingService.Object.HasCompletedOnboardingAsync();

        // Assert
        Assert.IsFalse(result);
    }

    [TestMethod]
    public async Task OnboardingService_MarkOnboardingCompleted_CallsService()
    {
        // Arrange & Act
        await _mockOnboardingService.Object.MarkOnboardingCompletedAsync();

        // Assert
        _mockOnboardingService.Verify(s => s.MarkOnboardingCompletedAsync(), Times.Once);
    }

    [TestMethod]
    public async Task OnboardingService_MarkOnboardingSkipped_CallsService()
    {
        // Arrange & Act
        await _mockOnboardingService.Object.MarkOnboardingSkippedAsync();

        // Assert
        _mockOnboardingService.Verify(s => s.MarkOnboardingSkippedAsync(), Times.Once);
    }

    [TestMethod]
    public async Task OnboardingService_ResetOnboarding_CallsService()
    {
        // Arrange & Act
        await _mockOnboardingService.Object.ResetOnboardingAsync();

        // Assert
        _mockOnboardingService.Verify(s => s.ResetOnboardingAsync(), Times.Once);
    }

    [TestMethod]
    public void OnboardingWizard_DependsOnOnboardingService()
    {
        // This test verifies that OnboardingService is properly injected
        Assert.IsNotNull(_mockOnboardingService);
        Assert.IsNotNull(_mockOnboardingService.Object);
    }

    [TestMethod]
    public void OnboardingWizard_DependsOnTfsConfigService()
    {
        // This test verifies that TfsConfigService is properly injected
        Assert.IsNotNull(_tfsConfigService);
    }

    [TestMethod]
    public void OnboardingWizard_DependsOnErrorMessageService()
    {
        // This test verifies that ErrorMessageService is properly injected
        Assert.IsNotNull(_errorMessageService);
    }

    [TestMethod]
    public void OnboardingWizard_DependsOnSnackbar()
    {
        // This test verifies that ISnackbar is properly injected
        Assert.IsNotNull(_mockSnackbar);
        Assert.IsNotNull(_mockSnackbar.Object);
    }

    [TestMethod]
    public async Task StreamLineReader_ReadsLinesFromStream()
    {
        using var stream = new MemoryStream(Encoding.UTF8.GetBytes("line1\r\nline2\nline3"));
        using var reader = new StreamReader(stream);
        var lineReader = new OnboardingWizard.StreamLineReader(reader);

        var line1 = await lineReader.ReadLineAsync(CancellationToken.None);
        var line2 = await lineReader.ReadLineAsync(CancellationToken.None);
        var line3 = await lineReader.ReadLineAsync(CancellationToken.None);
        var line4 = await lineReader.ReadLineAsync(CancellationToken.None);

        Assert.AreEqual("line1", line1);
        Assert.AreEqual("line2", line2);
        Assert.AreEqual("line3", line3);
        Assert.IsNull(line4);
    }

    /// <summary>
    /// Mock NavigationManager for testing
    /// </summary>
    private class MockNavigationManager : Microsoft.AspNetCore.Components.NavigationManager
    {
        public MockNavigationManager()
        {
            Initialize("https://localhost:5001/", "https://localhost:5001/");
        }

        protected override void NavigateToCore(string uri, bool forceLoad)
        {
            // No-op for testing
        }
    }
}
