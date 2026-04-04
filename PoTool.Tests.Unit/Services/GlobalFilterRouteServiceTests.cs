using Microsoft.VisualStudio.TestTools.UnitTesting;
using PoTool.Client.ApiClient;
using PoTool.Client.Models;
using PoTool.Client.Services;
using PoTool.Shared.Settings;
using System.Net;
using System.Net.Http;
using System.Net.Http.Json;

namespace PoTool.Tests.Unit.Services;

[TestClass]
public sealed class GlobalFilterRouteServiceTests
{
    [TestMethod]
    public async Task BuildCurrentPageUriAsync_PlanningOverview_EmitsAliasPathAndCanonicalQuery()
    {
        var routeService = CreateRouteService();

        var uri = await routeService.BuildCurrentPageUriAsync(
            "http://localhost/planning/payments-platform/overview?teamId=7",
            new FilterState([11], ["project-payments"], 7, new FilterTimeSelection(FilterTimeMode.Snapshot)));

        Assert.AreEqual("/planning/payments-platform/overview?projectAlias=payments-platform&projectId=project-payments&productId=11&teamId=7&timeMode=Snapshot", uri);
    }

    [TestMethod]
    public async Task BuildUriAsync_RollingState_EmitsExplicitRollingQuery()
    {
        var routeService = CreateRouteService();

        var uri = await routeService.BuildUriAsync(
            "/home/pull-requests",
            new FilterState(
                Array.Empty<int>(),
                Array.Empty<string>(),
                7,
                new FilterTimeSelection(FilterTimeMode.Rolling, RollingWindow: 30, RollingUnit: FilterTimeUnit.Days)));

        Assert.AreEqual("/home/pull-requests?teamId=7&timeMode=Rolling&rollingWindow=30&rollingUnit=Days", uri);
    }

    private static GlobalFilterRouteService CreateRouteService()
    {
        var httpClient = new HttpClient(new StubHttpMessageHandler())
        {
            BaseAddress = new Uri("http://localhost/")
        };
        var projectService = new ProjectService(new ProjectsClient(httpClient), httpClient);
        var projectIdentityMapper = new ProjectIdentityMapper(projectService);
        return new GlobalFilterRouteService(projectIdentityMapper);
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
                        Content = JsonContent.Create(new ProjectDto("project-payments", "payments-platform", "Payments Platform", new List<int> { 11 }))
                    });
                }
            }

            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.NotFound));
        }
    }
}
