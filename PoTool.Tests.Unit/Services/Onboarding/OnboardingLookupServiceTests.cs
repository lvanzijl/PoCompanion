using Microsoft.EntityFrameworkCore;
using Moq;
using PoTool.Api.Persistence;
using PoTool.Api.Persistence.Entities.Onboarding;
using PoTool.Api.Services.Onboarding;
using PoTool.Shared.Onboarding;

namespace PoTool.Tests.Unit.Services.Onboarding;

[TestClass]
public sealed class OnboardingLookupServiceTests
{
    private PoToolDbContext _dbContext = null!;
    private Mock<IOnboardingLiveLookupClient> _liveLookupClient = null!;
    private OnboardingLookupService _service = null!;

    [TestInitialize]
    public void Setup()
    {
        var options = new DbContextOptionsBuilder<PoToolDbContext>()
            .UseInMemoryDatabase($"OnboardingLookupServiceTests_{Guid.NewGuid()}")
            .Options;
        _dbContext = new PoToolDbContext(options);
        _liveLookupClient = new Mock<IOnboardingLiveLookupClient>(MockBehavior.Strict);
        _service = new OnboardingLookupService(_dbContext, _liveLookupClient.Object);
    }

    [TestCleanup]
    public void Cleanup()
    {
        _dbContext.Database.EnsureDeleted();
        _dbContext.Dispose();
    }

    [TestMethod]
    public async Task GetProjectsAsync_ReturnsNotFound_WhenConnectionMissing()
    {
        var result = await _service.GetProjectsAsync(null, 10, 0, CancellationToken.None);

        Assert.IsFalse(result.Succeeded);
        Assert.AreEqual(OnboardingErrorCode.NotFound, result.Error!.Code);
    }

    [TestMethod]
    public async Task GetProjectsAsync_DelegatesToLiveLookupClient_WhenConnectionExists()
    {
        var connection = new TfsConnection
        {
            ConnectionKey = "connection",
            OrganizationUrl = "https://dev.azure.com/example",
            AuthenticationMode = "Ntlm",
            TimeoutSeconds = 30,
            ApiVersion = "7.1"
        };
        _dbContext.OnboardingTfsConnections.Add(connection);
        await _dbContext.SaveChangesAsync();

        _liveLookupClient
            .Setup(client => client.GetProjectsAsync(It.Is<TfsConnection>(item => item.OrganizationUrl == connection.OrganizationUrl), "abc", 10, 0, It.IsAny<CancellationToken>()))
            .ReturnsAsync(OnboardingOperationResult<IReadOnlyList<ProjectLookupResultDto>>.Success(new[]
            {
                new ProjectLookupResultDto("project-1", "Project", null)
            }));

        var result = await _service.GetProjectsAsync("abc", 10, 0, CancellationToken.None);

        Assert.IsTrue(result.Succeeded);
        Assert.HasCount(1, result.Data!);
    }
}
