using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.AspNetCore.Routing.Patterns;
using PoTool.Api.Configuration;
using PoTool.Tests.Unit.Support;
using RouteIntent = PoTool.Api.Configuration.DataSourceModeConfiguration.RouteIntent;

namespace PoTool.Tests.Unit.Configuration;

[TestClass]
public sealed class DataSourceModeEndpointValidationTests
{
    [TestMethod]
    public void ValidateManagedEndpoints_MissingMetadata_Throws()
    {
        var endpoint = BuildEndpoint("/api/unclassified");

        var exception = Assert.ThrowsExactly<InvalidOperationException>(() =>
            DataSourceModeEndpointValidation.ValidateManagedEndpoints([endpoint]));

        StringAssert.Contains(exception.Message, "/api/unclassified");
        StringAssert.Contains(exception.Message, "missing DataSourceMode metadata");
    }

    [TestMethod]
    public void ValidateManagedEndpoints_AmbiguousMetadata_Throws()
    {
        var endpoint = BuildEndpoint(
            "/api/duplicate",
            new DataSourceModeMetadata(RouteIntent.LiveAllowed),
            new DataSourceModeMetadata(RouteIntent.CacheOnlyAnalyticalRead));

        var exception = Assert.ThrowsExactly<InvalidOperationException>(() =>
            DataSourceModeEndpointValidation.ValidateManagedEndpoints([endpoint]));

        StringAssert.Contains(exception.Message, "/api/duplicate");
        StringAssert.Contains(exception.Message, "multiple DataSourceMode metadata entries");
    }

    [TestMethod]
    public void ValidateManagedEndpoints_ConfiguredApplication_PassesWithMetadataCoverage()
    {
        using var app = EndpointMetadataTestHostFactory.CreateConfiguredApplication();

        DataSourceModeEndpointValidation.ValidateManagedEndpoints(
            EndpointMetadataTestHostFactory.GetManagedRouteEndpoints(app));
    }

    private static RouteEndpoint BuildEndpoint(string pattern, params object[] metadata)
    {
        var builder = new RouteEndpointBuilder(
            context => Task.CompletedTask,
            RoutePatternFactory.Parse(pattern),
            order: 0);

        foreach (var item in metadata)
        {
            builder.Metadata.Add(item);
        }

        return (RouteEndpoint)builder.Build();
    }
}
