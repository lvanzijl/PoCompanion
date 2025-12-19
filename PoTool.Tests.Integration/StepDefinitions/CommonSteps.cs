using System.Net;
using Reqnroll;

namespace PoTool.Tests.Integration.StepDefinitions;

[Binding]
public class CommonSteps
{
    private readonly ScenarioContext _scenarioContext;

    public CommonSteps(ScenarioContext scenarioContext)
    {
        _scenarioContext = scenarioContext;
    }

    [Then(@"the response should be (.*)")]
    public void ThenTheResponseShouldBe(string expectedStatus)
    {
        var response = _scenarioContext.Get<HttpResponseMessage>("Response");
        Assert.IsNotNull(response);
        
        var expected = Enum.Parse<HttpStatusCode>(expectedStatus);
        Assert.AreEqual(expected, response.StatusCode, 
            $"Expected {expected} but got {response.StatusCode}");
    }
}
