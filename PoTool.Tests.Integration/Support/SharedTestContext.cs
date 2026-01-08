using Microsoft.AspNetCore.Mvc.Testing;

namespace PoTool.Tests.Integration.Support;

/// <summary>
/// Shared context for integration tests that provides a single WebApplicationFactory instance
/// to be reused across all scenarios, improving test performance.
/// </summary>
public class SharedTestContext : IDisposable
{
    private static readonly Lazy<IntegrationTestWebApplicationFactory> _lazyFactory = 
        new(() => new IntegrationTestWebApplicationFactory());

    /// <summary>
    /// Gets the shared WebApplicationFactory instance that is reused across all test scenarios.
    /// </summary>
    public IntegrationTestWebApplicationFactory Factory => _lazyFactory.Value;

    public void Dispose()
    {
        // Factory disposal is handled by the test framework at the end of the test run
        // We don't dispose here to allow factory reuse across scenarios
    }
}
