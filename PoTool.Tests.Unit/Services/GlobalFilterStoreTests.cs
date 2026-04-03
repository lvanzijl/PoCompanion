using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using PoTool.Client.Models;
using PoTool.Client.Services;
using PoTool.Shared.Settings;
using System.Net;
using System.Net.Http;
using System.Net.Http.Json;

namespace PoTool.Tests.Unit.Services;

[TestClass]
public sealed class GlobalFilterStoreTests
{
    [TestMethod]
    public async Task TrackNavigation_UnknownRoute_ResetsToNeutral()
    {
        var store = CreateStore();

        await store.TrackNavigationAsync("http://localhost/settings");

        Assert.IsNull(store.CurrentUsage);
        Assert.IsTrue(store.CurrentState.AllProducts);
        Assert.IsTrue(store.CurrentState.AllProjects);
        Assert.AreEqual(FilterTimeMode.Snapshot, store.CurrentState.Time.Mode);
    }

    [TestMethod]
    public async Task TrackNavigation_ProjectScopedPlanningRoute_UsesProjectIdFromPathAlias()
    {
        var store = CreateStore();

        await store.TrackNavigationAsync("http://localhost/planning/payments-platform/overview", 42);

        Assert.IsNotNull(store.CurrentUsage);
        Assert.AreEqual("ProjectPlanningOverview", store.CurrentUsage.PageName);
        CollectionAssert.AreEqual(new[] { "project-payments" }, store.CurrentState.ProjectIds.ToArray());
        Assert.AreEqual(42, store.CurrentUsage.ActiveProfileId);
        Assert.AreEqual(FilterUpdateSource.Route, store.CurrentUsage.LastUpdateSource);
    }

    [TestMethod]
    public async Task TrackNavigation_SprintRouteWithoutSelections_FlagsMissingTeamAndSprint()
    {
        var store = CreateStore();

        await store.TrackNavigationAsync("http://localhost/home/delivery/sprint");

        Assert.IsNotNull(store.CurrentUsage);
        Assert.AreEqual("SprintTrend", store.CurrentUsage.PageName);
        Assert.IsTrue(store.CurrentUsage.MissingTeam);
        Assert.IsTrue(store.CurrentUsage.MissingSprint);
    }

    [TestMethod]
    public async Task TrackNavigation_QueryBackedRoute_TracksKnownFilterValues()
    {
        var store = CreateStore();

        await store.TrackNavigationAsync("http://localhost/home/trends?productId=5&teamId=7&fromSprintId=101&toSprintId=100");

        Assert.IsNotNull(store.CurrentUsage);
        CollectionAssert.AreEqual(new[] { 5 }, store.CurrentState.ProductIds.ToArray());
        Assert.AreEqual(7, store.CurrentState.TeamId);
        Assert.AreEqual(FilterTimeMode.Range, store.CurrentState.Time.Mode);
        Assert.AreEqual(100, store.CurrentState.Time.StartSprintId);
        Assert.AreEqual(101, store.CurrentState.Time.EndSprintId);
        Assert.IsFalse(store.CurrentUsage.MissingTeam);
        Assert.IsFalse(store.CurrentUsage.MissingSprint);
        CollectionAssert.Contains(store.CurrentUsage.NormalizationDecisions.ToArray(), "invalid range normalized by swapping from/to sprint IDs");
    }

    private static GlobalFilterStore CreateStore()
    {
        var handler = new StubHttpMessageHandler();
        var httpClient = new HttpClient(handler)
        {
            BaseAddress = new Uri("http://localhost/")
        };

        var projectService = new ProjectService(httpClient);
        var resolver = new FilterStateResolver(projectService);
        return new GlobalFilterStore(NullLogger<GlobalFilterStore>.Instance, resolver);
    }

    private sealed class StubHttpMessageHandler : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            var path = request.RequestUri?.AbsolutePath ?? string.Empty;
            if (path.StartsWith("/api/projects/", StringComparison.OrdinalIgnoreCase))
            {
                var key = Uri.UnescapeDataString(path["/api/projects/".Length..]);
                if (string.Equals(key, "payments-platform", StringComparison.OrdinalIgnoreCase)
                    || string.Equals(key, "project-payments", StringComparison.OrdinalIgnoreCase))
                {
                    return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
                    {
                        Content = JsonContent.Create(new ProjectDto("project-payments", "payments-platform", "Payments Platform", new List<int> { 11, 12 }))
                    });
                }
            }

            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.NotFound));
        }
    }
}
