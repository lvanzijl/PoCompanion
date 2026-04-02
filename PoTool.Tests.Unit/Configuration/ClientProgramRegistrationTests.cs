using Microsoft.VisualStudio.TestTools.UnitTesting;
using Microsoft.Extensions.DependencyInjection;
using PoTool.Client.ApiClient;

namespace PoTool.Tests.Unit.Configuration;

[TestClass]
public class ClientProgramRegistrationTests
{
    [TestMethod]
    public void AddBugTriageClient_RegistersResolvableBugTriageClient()
    {
        var services = new ServiceCollection();
        services.AddScoped(_ => new HttpClient());

        services.AddBugTriageClient("http://localhost:5291");

        using var provider = services.BuildServiceProvider();
        using var scope = provider.CreateScope();

        var client = scope.ServiceProvider.GetService<IBugTriageClient>();

        Assert.IsNotNull(client, "Bugs Triage route requires IBugTriageClient to resolve from DI.");
        Assert.IsInstanceOfType<BugTriageClient>(client, "IBugTriageClient should resolve to the generated BugTriageClient.");
    }
}
