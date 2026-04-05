namespace PoTool.Tests.Unit.Audits;

[TestClass]
public sealed class PageContextContractEnforcementAuditTests
{
    [TestMethod]
    public void PortfolioDelivery_Page_ForwardsSelectedProductScope()
    {
        var portfolioDelivery = File.ReadAllText(Path.Combine(GetRepositoryRoot(), "PoTool.Client", "Pages", "Home", "PortfolioDelivery.razor"));

        StringAssert.Contains(portfolioDelivery, "GlobalFilterStore.GetState().ProductIds.Count > 0 ? GlobalFilterStore.GetState().ProductIds : null");
        Assert.DoesNotContain(portfolioDelivery, "productIds: null");
    }

    [TestMethod]
    public void PipelineInsights_Wiring_ForwardsProductScopeAcrossClientAndApi()
    {
        var repositoryRoot = GetRepositoryRoot();
        var page = File.ReadAllText(Path.Combine(repositoryRoot, "PoTool.Client", "Pages", "Home", "PipelineInsights.razor"));
        var stateService = File.ReadAllText(Path.Combine(repositoryRoot, "PoTool.Client", "Services", "PipelineStateService.cs"));
        var apiClient = File.ReadAllText(Path.Combine(repositoryRoot, "PoTool.Client", "ApiClient", "ApiClient.PipelineFilters.cs"));
        var controller = File.ReadAllText(Path.Combine(repositoryRoot, "PoTool.Api", "Controllers", "PipelinesController.cs"));

        StringAssert.Contains(page, "GlobalFilterStore.GetState().ProductIds.Count > 0 ? GlobalFilterStore.GetState().ProductIds : null");
        StringAssert.Contains(stateService, "IEnumerable<int>? productIds");
        StringAssert.Contains(stateService, "_pipelinesClient.GetInsightsAsync(productOwnerId, sprintId, productIds, includePartiallySucceeded, includeCanceled, cancellationToken)");
        StringAssert.Contains(apiClient, "ICollection<int>? productIds");
        StringAssert.Contains(apiClient, "GetInsightsAsync(productOwnerId, sprintId, productIds, includePartiallySucceeded, includeCanceled, cancellationToken)");
        StringAssert.Contains(controller, "[FromQuery] int[]? productIds = null");
        StringAssert.Contains(controller, "ProductIds: productIds");
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

        throw new DirectoryNotFoundException("Could not locate repository root.");
    }
}
