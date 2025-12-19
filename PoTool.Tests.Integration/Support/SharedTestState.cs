namespace PoTool.Tests.Integration.Support;

/// <summary>
/// Shared context for scenario state across step definitions.
/// </summary>
public class SharedTestState
{
    public IntegrationTestWebApplicationFactory Factory { get; }
    public HttpClient Client { get; }
    public HttpResponseMessage? Response { get; set; }

    public SharedTestState()
    {
        Factory = new IntegrationTestWebApplicationFactory();
        Client = Factory.CreateClient();
    }
}
