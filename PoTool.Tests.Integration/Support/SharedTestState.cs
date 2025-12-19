namespace PoTool.Tests.Integration.Support;

/// <summary>
/// Shared context for scenario state across step definitions.
/// Implements IDisposable to properly clean up resources.
/// </summary>
public class SharedTestState : IDisposable
{
    public IntegrationTestWebApplicationFactory Factory { get; }
    public HttpClient Client { get; }
    public HttpResponseMessage? Response { get; set; }

    public SharedTestState()
    {
        Factory = new IntegrationTestWebApplicationFactory();
        Client = Factory.CreateClient();
    }

    public void Dispose()
    {
        Client?.Dispose();
        Factory?.Dispose();
    }
}
