using System.Net;
using Reqnroll;

using PoTool.Core.WorkItems;

using PoTool.Core.Settings;

namespace PoTool.Tests.Integration.StepDefinitions;

[Binding]
public class CommonSteps
{
    private readonly ScenarioContext _scenarioContext;

    public CommonSteps(ScenarioContext scenarioContext)
    {
        _scenarioContext = scenarioContext;
    }

    [Given(@"the application is running")]
    public void GivenTheApplicationIsRunning()
    {
        // Application is already running via the factory in each step class
        // This is a shared step that does nothing but confirms the app is available
    }

    private void AssertResponseStatus(HttpStatusCode expected)
    {
        var response = _scenarioContext.Get<HttpResponseMessage>("Response");
        Assert.IsNotNull(response);
        Assert.AreEqual(expected, response.StatusCode,
            $"Expected {expected} but got {response.StatusCode}");
    }

    [Then(@"the response should be OK")]
    public void ThenTheResponseShouldBeOK()
    {
        AssertResponseStatus(HttpStatusCode.OK);
    }

    [Then(@"the response should be Created")]
    public void ThenTheResponseShouldBeCreated()
    {
        AssertResponseStatus(HttpStatusCode.Created);
    }

    [Then(@"the response should be NoContent")]
    public void ThenTheResponseShouldBeNoContent()
    {
        AssertResponseStatus(HttpStatusCode.NoContent);
    }

    [Then(@"the response should be NotFound")]
    public void ThenTheResponseShouldBeNotFound()
    {
        AssertResponseStatus(HttpStatusCode.NotFound);
    }

    [Then(@"the response should be BadRequest")]
    public void ThenTheResponseShouldBeBadRequest()
    {
        AssertResponseStatus(HttpStatusCode.BadRequest);
    }

    [Then(@"the response should be Unauthorized")]
    public void ThenTheResponseShouldBeUnauthorized()
    {
        AssertResponseStatus(HttpStatusCode.Unauthorized);
    }

    [Then(@"the response should be Accepted")]
    public void ThenTheResponseShouldBeAccepted()
    {
        AssertResponseStatus(HttpStatusCode.Accepted);
    }

    [Then(@"the response should be InternalServerError")]
    public void ThenTheResponseShouldBeInternalServerError()
    {
        AssertResponseStatus(HttpStatusCode.InternalServerError);
    }

    [Then(@"the response should be ServiceUnavailable")]
    public void ThenTheResponseShouldBeServiceUnavailable()
    {
        AssertResponseStatus(HttpStatusCode.ServiceUnavailable);
    }

    [Then(@"the response should be Conflict")]
    public void ThenTheResponseShouldBeConflict()
    {
        AssertResponseStatus(HttpStatusCode.Conflict);
    }

    [Then(@"the response should be MethodNotAllowed")]
    public void ThenTheResponseShouldBeMethodNotAllowed()
    {
        AssertResponseStatus(HttpStatusCode.MethodNotAllowed);
    }

    [Then(@"the response should be UnsupportedMediaType")]
    public void ThenTheResponseShouldBeUnsupportedMediaType()
    {
        AssertResponseStatus(HttpStatusCode.UnsupportedMediaType);
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
