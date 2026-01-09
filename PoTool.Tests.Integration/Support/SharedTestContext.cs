using Microsoft.AspNetCore.Mvc.Testing;

namespace PoTool.Tests.Integration.Support;

/// <summary>
/// Shared context for integration tests that provides a single WebApplicationFactory instance
/// to be reused across all scenarios, improving test performance.
/// Note: The factory is kept alive for the duration of the test run to enable reuse across scenarios.
/// </summary>
public class SharedTestContext
{
    private static readonly Lazy<IntegrationTestWebApplicationFactory> _lazyFactory = 
        new(() => new IntegrationTestWebApplicationFactory());

    /// <summary>
    /// Gets the shared WebApplicationFactory instance that is reused across all test scenarios.
    /// </summary>
    public IntegrationTestWebApplicationFactory Factory => _lazyFactory.Value;
}
