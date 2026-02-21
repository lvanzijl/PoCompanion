using Microsoft.EntityFrameworkCore;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using PoTool.Api.Persistence;
using PoTool.Api.Persistence.Entities;
using PoTool.Api.Repositories;

namespace PoTool.Tests.Unit.Repositories;

/// <summary>
/// Tests for Product-Team linking functionality in ProductRepository.
/// Verifies that onboarding wizard team-product linking works correctly.
/// </summary>
[TestClass]
public class ProductTeamLinkRepositoryTests
{
    private PoToolDbContext _context = null!;
    private ProductRepository _productRepository = null!;
    private TeamRepository _teamRepository = null!;
    private ProfileRepository _profileRepository = null!;

    [TestInitialize]
    public void Setup()
    {
        // Use in-memory SQLite database for real query translation testing
        var options = new DbContextOptionsBuilder<PoToolDbContext>()
            .UseSqlite("Data Source=:memory:", sqliteOptions =>
            {
                sqliteOptions.UseQuerySplittingBehavior(QuerySplittingBehavior.SplitQuery);
            })
            .Options;
        _context = new PoToolDbContext(options);
        // Important: Keep connection open for in-memory database to persist
        _context.Database.OpenConnection();
        _context.Database.EnsureCreated();

        _productRepository = new ProductRepository(_context);
        _teamRepository = new TeamRepository(_context);
        _profileRepository = new ProfileRepository(_context);
    }

    [TestCleanup]
    public void Cleanup()
    {
        // Close connection to dispose of in-memory database
        _context.Database.CloseConnection();
        _context.Dispose();
    }

    /// <summary>
    /// Verifies that LinkTeamAsync creates a ProductTeamLink entity.
    /// This simulates the onboarding wizard scenario where a team is linked to a product.
    /// </summary>
    [TestMethod]
    public async Task LinkTeamAsync_CreatesProductTeamLink_Successfully()
    {
        // Arrange - Create profile
        var profile = await _profileRepository.CreateProfileAsync("Test Owner", new List<int>());
        
        // Create product (simulating onboarding wizard SaveProduct)
        var product = await _productRepository.CreateProductAsync(
            productOwnerId: profile.Id,
            name: "Test Product",
            backlogRootWorkItemId: 12345,
            pictureType: Shared.Settings.ProductPictureType.Default,
            defaultPictureId: 0,
            customPicturePath: null
        );

        // Create team (simulating onboarding wizard SaveTeam)
        var team = await _teamRepository.CreateTeamAsync(
            name: "Test Team",
            teamAreaPath: "TestProject\\TestTeam",
            pictureType: Shared.Settings.TeamPictureType.Default,
            defaultPictureId: 0,
            customPicturePath: null,
            projectName: "TestProject",
            tfsTeamId: "team-guid-123",
            tfsTeamName: "Test Team"
        );

        // Act - Link team to product (simulating onboarding wizard after SaveTeam)
        var result = await _productRepository.LinkTeamAsync(product.Id, team.Id);

        // Assert
        Assert.IsTrue(result, "LinkTeamAsync should return true");

        // Verify the link was created in database
        var link = await _context.ProductTeamLinks
            .FirstOrDefaultAsync(l => l.ProductId == product.Id && l.TeamId == team.Id);
        Assert.IsNotNull(link, "ProductTeamLink should exist in database");

        // Verify product now includes the team ID
        var updatedProduct = await _productRepository.GetProductByIdAsync(product.Id);
        Assert.IsNotNull(updatedProduct, "Product should be retrievable");
        Assert.HasCount(1, updatedProduct.TeamIds, "Product should have 1 linked team");
        Assert.AreEqual(team.Id, updatedProduct.TeamIds[0], "Product should be linked to the correct team");
    }

    /// <summary>
    /// Verifies that LinkTeamAsync is idempotent (doesn't create duplicates).
    /// </summary>
    [TestMethod]
    public async Task LinkTeamAsync_WhenAlreadyLinked_IsIdempotent()
    {
        // Arrange
        var profile = await _profileRepository.CreateProfileAsync("Test Owner", new List<int>());
        var product = await _productRepository.CreateProductAsync(
            productOwnerId: profile.Id,
            name: "Test Product",
            backlogRootWorkItemId: 12345,
            pictureType: Shared.Settings.ProductPictureType.Default,
            defaultPictureId: 0,
            customPicturePath: null
        );
        var team = await _teamRepository.CreateTeamAsync(
            name: "Test Team",
            teamAreaPath: "TestProject\\TestTeam",
            pictureType: Shared.Settings.TeamPictureType.Default,
            defaultPictureId: 0,
            customPicturePath: null,
            projectName: "TestProject",
            tfsTeamId: "team-guid-123",
            tfsTeamName: "Test Team"
        );

        // Link once
        await _productRepository.LinkTeamAsync(product.Id, team.Id);

        // Act - Link again
        var result = await _productRepository.LinkTeamAsync(product.Id, team.Id);

        // Assert
        Assert.IsTrue(result, "Second LinkTeamAsync should return true");

        // Verify only one link exists
        var linkCount = await _context.ProductTeamLinks
            .CountAsync(l => l.ProductId == product.Id && l.TeamId == team.Id);
        Assert.AreEqual(1, linkCount, "Should only have one ProductTeamLink (no duplicates)");
    }

    /// <summary>
    /// Verifies that a product can be linked to multiple teams.
    /// </summary>
    [TestMethod]
    public async Task LinkTeamAsync_MultipleTeams_AllLinkedCorrectly()
    {
        // Arrange
        var profile = await _profileRepository.CreateProfileAsync("Test Owner", new List<int>());
        var product = await _productRepository.CreateProductAsync(
            productOwnerId: profile.Id,
            name: "Test Product",
            backlogRootWorkItemId: 12345,
            pictureType: Shared.Settings.ProductPictureType.Default,
            defaultPictureId: 0,
            customPicturePath: null
        );

        var team1 = await _teamRepository.CreateTeamAsync("Team 1", "TestProject\\Team1",
            Shared.Settings.TeamPictureType.Default, 0, null, "TestProject", "team-1", "Team 1");
        var team2 = await _teamRepository.CreateTeamAsync("Team 2", "TestProject\\Team2",
            Shared.Settings.TeamPictureType.Default, 1, null, "TestProject", "team-2", "Team 2");

        // Act
        await _productRepository.LinkTeamAsync(product.Id, team1.Id);
        await _productRepository.LinkTeamAsync(product.Id, team2.Id);

        // Assert
        var updatedProduct = await _productRepository.GetProductByIdAsync(product.Id);
        Assert.IsNotNull(updatedProduct);
        Assert.HasCount(2, updatedProduct.TeamIds, "Product should have 2 linked teams");
        CollectionAssert.Contains(updatedProduct.TeamIds, team1.Id, "Product should contain team1");
        CollectionAssert.Contains(updatedProduct.TeamIds, team2.Id, "Product should contain team2");
    }
}
