namespace PoTool.Tests.Integration.Support;

/// <summary>
/// Shared test context that provides a WebApplicationFactory instance per scenario.
/// This eliminates the overhead of creating a new web server for each step definition
/// class within the same scenario, while still maintaining database isolation between scenarios.
/// The factory is lazily initialized on first access within each scenario and reused
/// by all step definition classes in that scenario, significantly improving test performance.
/// </summary>
public class SharedTestContext
{
    private readonly Lazy<IntegrationTestWebApplicationFactory> _lazyFactory = 
        new(() => new IntegrationTestWebApplicationFactory());

    /// <summary>
    /// Gets the WebApplicationFactory instance for this scenario.
    /// The factory is lazily initialized on first access and reused for all step classes in the scenario.
    /// Each scenario gets its own factory instance to ensure database isolation.
    /// </summary>
    public IntegrationTestWebApplicationFactory Factory => _lazyFactory.Value;
}
