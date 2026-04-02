using System.Reflection;
using PoTool.Api.Persistence;

namespace PoTool.Tests.Unit.Audits;

[TestClass]
public sealed class PersistenceContractAuditTests
{
    [TestMethod]
    public void PoToolDbContext_OverridesAllSaveChangesEntrypoints_AndCallsValidator()
    {
        var dbContextType = typeof(PoToolDbContext);
        var saveChanges = dbContextType.GetMethod(nameof(PoToolDbContext.SaveChanges), Type.EmptyTypes);
        var saveChangesWithAcceptAll = dbContextType.GetMethod(nameof(PoToolDbContext.SaveChanges), [typeof(bool)]);
        var saveChangesAsync = dbContextType.GetMethod(nameof(PoToolDbContext.SaveChangesAsync), [typeof(CancellationToken)]);
        var saveChangesAsyncWithAcceptAll = dbContextType.GetMethod(nameof(PoToolDbContext.SaveChangesAsync), [typeof(bool), typeof(CancellationToken)]);

        Assert.AreEqual(dbContextType, saveChanges?.DeclaringType, "PoToolDbContext must override SaveChanges().");
        Assert.AreEqual(dbContextType, saveChangesWithAcceptAll?.DeclaringType, "PoToolDbContext must override SaveChanges(bool).");
        Assert.AreEqual(dbContextType, saveChangesAsync?.DeclaringType, "PoToolDbContext must override SaveChangesAsync(CancellationToken).");
        Assert.AreEqual(dbContextType, saveChangesAsyncWithAcceptAll?.DeclaringType, "PoToolDbContext must override SaveChangesAsync(bool, CancellationToken).");

        var source = File.ReadAllText(Path.Combine(GetRepositoryRoot(), "PoTool.Api", "Persistence", "PoToolDbContext.cs"));
        StringAssert.Contains(source, "RequiredRelationshipPersistenceValidator.ValidatePendingRequiredRelationships");
        StringAssert.Contains(source, "RequiredRelationshipPersistenceValidator.ValidatePendingRequiredRelationshipsAsync");
    }

    [TestMethod]
    public void MockConfigurationSeedHostedService_RetainsPlanValidateExecuteStructure()
    {
        var source = File.ReadAllText(Path.Combine(
            GetRepositoryRoot(),
            "PoTool.Api",
            "Services",
            "MockData",
            "MockConfigurationSeedHostedService.cs"));

        StringAssert.Contains(source, "BuildSeedPlan(hierarchy)");
        StringAssert.Contains(source, "BeginTransactionAsync");
        StringAssert.Contains(source, "SaveChangesWithDiagnosticsAsync");
        StringAssert.Contains(source, "dependency-ordered");
    }

    [TestMethod]
    public void CopilotInstructions_DefineGlobalPersistenceContract()
    {
        var source = File.ReadAllText(Path.Combine(GetRepositoryRoot(), ".github", "copilot-instructions.md"));

        StringAssert.Contains(source, "Global persistence contract");
        StringAssert.Contains(source, "required foreign keys");
        StringAssert.Contains(source, "validation before persistence");
    }

    private static string GetRepositoryRoot()
    {
        var current = new DirectoryInfo(AppContext.BaseDirectory);

        while (current is not null)
        {
            if (File.Exists(Path.Combine(current.FullName, "PoTool.sln")))
            {
                return current.FullName;
            }

            current = current.Parent;
        }

        throw new DirectoryNotFoundException("Could not locate the repository root containing PoTool.sln.");
    }
}
