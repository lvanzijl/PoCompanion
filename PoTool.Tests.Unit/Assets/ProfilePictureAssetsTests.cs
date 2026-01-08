using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace PoTool.Tests.Unit.Assets;

/// <summary>
/// Tests to verify that required profile picture assets exist as specified in User_landing_v2.md.
/// </summary>
[TestClass]
public class ProfilePictureAssetsTests
{
    private const string AssetsPath = "PoTool.Client/wwwroot/assets/profile-defaults";
    private const int ExpectedProfilePictureCount = 64;

    [TestMethod]
    public void ProfilePictureAssets_Has64DefaultImages()
    {
        // Arrange
        var baseDir = FindRepositoryRoot();
        var assetsDir = Path.Combine(baseDir, AssetsPath);

        // Act
        var files = Directory.GetFiles(assetsDir, "profile-*.svg");

        // Assert
        Assert.AreEqual(ExpectedProfilePictureCount, files.Length, 
            $"Expected {ExpectedProfilePictureCount} default profile pictures");
    }

    [TestMethod]
    public void ProfilePictureAssets_AllHaveCorrectNaming()
    {
        // Arrange
        var baseDir = FindRepositoryRoot();
        var assetsDir = Path.Combine(baseDir, AssetsPath);

        // Act
        var files = Directory.GetFiles(assetsDir, "profile-*.svg");
        var fileNames = files.Select(f => Path.GetFileName(f)).OrderBy(n => n).ToList();

        // Assert
        for (int i = 0; i < ExpectedProfilePictureCount; i++)
        {
            var expectedName = $"profile-{i}.svg";
            CollectionAssert.Contains(fileNames, expectedName, 
                $"Missing profile picture: {expectedName}");
        }
    }

    [TestMethod]
    public void ProfilePictureAssets_AllFilesAreValid()
    {
        // Arrange
        var baseDir = FindRepositoryRoot();
        var assetsDir = Path.Combine(baseDir, AssetsPath);

        // Act
        var files = Directory.GetFiles(assetsDir, "profile-*.svg");

        // Assert
        foreach (var file in files)
        {
            var fileInfo = new FileInfo(file);
            Assert.IsGreaterThan(fileInfo.Length, 0L, 
                $"Profile picture {fileInfo.Name} is empty");
            Assert.IsLessThan(fileInfo.Length, 10000L, 
                $"Profile picture {fileInfo.Name} is unexpectedly large (should be a simple SVG)");
        }
    }

    private string FindRepositoryRoot()
    {
        // Start from current directory and walk up to find the repository root
        var currentDir = Directory.GetCurrentDirectory();
        while (currentDir != null)
        {
            if (Directory.Exists(Path.Combine(currentDir, ".git")) ||
                File.Exists(Path.Combine(currentDir, "PoTool.sln")))
            {
                return currentDir;
            }
            currentDir = Directory.GetParent(currentDir)?.FullName;
        }
        
        // If not found, assume we're running from the repository root
        return Directory.GetCurrentDirectory();
    }
}
