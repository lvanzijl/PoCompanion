using Microsoft.AspNetCore.Http;
using PoTool.Api.Configuration;
using PoTool.Tests.Unit.Support;

namespace PoTool.Tests.Unit.Audits;

[TestCategory("Governance")]
[TestClass]
public sealed class DataSourceRouteClassificationAuditTests
{
    [TestMethod]
    public void ManagedEndpoints_DeclareExactlyOneDataSourceModeMetadataEntry()
    {
        using var app = EndpointMetadataTestHostFactory.CreateConfiguredApplication();

        var violations = EndpointMetadataTestHostFactory.GetManagedRouteEndpoints(app)
            .Select(endpoint => new
            {
                Path = EndpointMetadataTestHostFactory.NormalizePath(endpoint.RoutePattern.RawText),
                MetadataCount = endpoint.Metadata.GetOrderedMetadata<IDataSourceModeMetadata>().Count
            })
            .Where(entry => entry.MetadataCount != 1)
            .Select(entry => $"{entry.Path} => metadata-count={entry.MetadataCount}")
            .ToArray();

        CollectionAssert.AreEqual(Array.Empty<string>(), violations);
    }
}
