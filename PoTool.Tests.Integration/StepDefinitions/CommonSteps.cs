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

    [Then(@"the response should be (OK|Created|NoContent|NotFound|BadRequest|Unauthorized|Accepted|InternalServerError|ServiceUnavailable|Conflict|MethodNotAllowed|UnsupportedMediaType)")]
    public void ThenTheResponseShouldBe(string expectedStatus)
    {
        var response = _scenarioContext.Get<HttpResponseMessage>("Response");
        Assert.IsNotNull(response);
        
        var expected = Enum.Parse<HttpStatusCode>(expectedStatus);
        Assert.AreEqual(expected, response.StatusCode, 
            $"Expected {expected} but got {response.StatusCode}");
    }

    [Then(@"the response should be successful")]
    public void ThenTheResponseShouldBeSuccessful()
    {
        var response = _scenarioContext.Get<HttpResponseMessage>("Response");
        Assert.IsNotNull(response);
        Assert.IsTrue(response.IsSuccessStatusCode, 
            $"Expected success status but got {response.StatusCode}");
    }
}
