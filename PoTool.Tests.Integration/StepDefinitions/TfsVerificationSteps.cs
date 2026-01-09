using System.Net;
using System.Net.Http.Json;
using PoTool.Api.Configuration;
using PoTool.Shared.Contracts.TfsVerification;
using PoTool.Tests.Integration.Support;
using Reqnroll;

namespace PoTool.Tests.Integration.StepDefinitions;

[Binding]
public class TfsVerificationSteps
{
    private readonly HttpClient _client;
    private HttpResponseMessage _response = null!;
    private TfsVerificationReport? _verificationReport;
    private TfsVerifyRequest? _verifyRequest;

    public TfsVerificationSteps(SharedTestContext sharedContext)
    {
        // Use shared factory to avoid creating a new web server per step class
        _client = sharedContext.Factory.CreateClient();
    }

    [When(@"I request TFS API verification with read-only checks")]
    public async Task WhenIRequestTfsApiVerificationWithReadOnlyChecks()
    {
        _verifyRequest = new TfsVerifyRequest(
            IncludeWriteChecks: false,
            WorkItemIdForWriteCheck: null
        );

        _response = await _client.PostAsJsonAsync("/api/tfsverify", _verifyRequest);
        
        if (_response.IsSuccessStatusCode)
        {
            _verificationReport = await _response.Content.ReadFromJsonAsync<TfsVerificationReport>();
        }
    }

    [When(@"I request TFS API verification with write checks for work item (.*)")]
    public async Task WhenIRequestTfsApiVerificationWithWriteChecksForWorkItem(int workItemId)
    {
        _verifyRequest = new TfsVerifyRequest(
            IncludeWriteChecks: true,
            WorkItemIdForWriteCheck: workItemId
        );

        _response = await _client.PostAsJsonAsync("/api/tfsverify", _verifyRequest);
        
        if (_response.IsSuccessStatusCode)
        {
            _verificationReport = await _response.Content.ReadFromJsonAsync<TfsVerificationReport>();
        }
    }

    [When(@"I request TFS API verification with write checks but no work item ID")]
    public async Task WhenIRequestTfsApiVerificationWithWriteChecksButNoWorkItemId()
    {
        _verifyRequest = new TfsVerifyRequest(
            IncludeWriteChecks: true,
            WorkItemIdForWriteCheck: null
        );

        _response = await _client.PostAsJsonAsync("/api/tfsverify", _verifyRequest);
        
        if (_response.IsSuccessStatusCode)
        {
            _verificationReport = await _response.Content.ReadFromJsonAsync<TfsVerificationReport>();
        }
    }

    [Then(@"the verification response should be OK")]
    public void ThenTheVerificationResponseShouldBeOk()
    {
        Assert.AreEqual(HttpStatusCode.OK, _response.StatusCode);
        Assert.IsNotNull(_verificationReport);
    }

    [Then(@"the verification report should include read-only checks")]
    public void ThenTheVerificationReportShouldIncludeReadOnlyChecks()
    {
        Assert.IsNotNull(_verificationReport);
        Assert.IsFalse(_verificationReport.IncludedWriteChecks);
        
        var checkCount = _verificationReport.Checks.Count;
        var checkIds = string.Join(", ", _verificationReport.Checks.Select(c => c.CapabilityId));
        Assert.IsTrue(checkCount >= 7, $"Expected at least 7 read-only checks, but found {checkCount}. Checks: {checkIds}");
    }

    [Then(@"the verification report should include write checks")]
    public void ThenTheVerificationReportShouldIncludeWriteChecks()
    {
        Assert.IsNotNull(_verificationReport);
        Assert.IsTrue(_verificationReport.IncludedWriteChecks);
        Assert.IsTrue(_verificationReport.Checks.Any(c => c.CapabilityId == "work-item-update"));
    }

    [Then(@"the verification report should not include write checks")]
    public void ThenTheVerificationReportShouldNotIncludeWriteChecks()
    {
        Assert.IsNotNull(_verificationReport);
        Assert.IsFalse(_verificationReport.Checks.Any(c => c.CapabilityId == "work-item-update"));
    }

    [Then(@"all checks should pass")]
    public void ThenAllChecksShouldPass()
    {
        Assert.IsNotNull(_verificationReport);
        Assert.IsTrue(_verificationReport.Success);
        Assert.IsTrue(_verificationReport.Checks.All(c => c.Success));
    }

    [Then(@"the write check should target work item (.*)")]
    public void ThenTheWriteCheckShouldTargetWorkItem(int workItemId)
    {
        Assert.IsNotNull(_verificationReport);
        var writeCheck = _verificationReport.Checks.FirstOrDefault(c => c.CapabilityId == "work-item-update");
        Assert.IsNotNull(writeCheck);
        Assert.AreEqual($"Work Item #{workItemId}", writeCheck.TargetScope);
    }

    [Then(@"the verification report should include capability ""(.*)""")]
    public void ThenTheVerificationReportShouldIncludeCapability(string capabilityId)
    {
        Assert.IsNotNull(_verificationReport);
        var check = _verificationReport.Checks.FirstOrDefault(c => c.CapabilityId == capabilityId);
        Assert.IsNotNull(check, $"Expected capability '{capabilityId}' not found in verification report");
    }

    [Then(@"the verification report should have server URL ""(.*)""")]
    public void ThenTheVerificationReportShouldHaveServerUrl(string expectedUrl)
    {
        Assert.IsNotNull(_verificationReport);
        Assert.AreEqual(expectedUrl, _verificationReport.ServerUrl);
    }

    [Then(@"the verification report should have project name ""(.*)""")]
    public void ThenTheVerificationReportShouldHaveProjectName(string expectedProject)
    {
        Assert.IsNotNull(_verificationReport);
        Assert.AreEqual(expectedProject, _verificationReport.ProjectName);
    }

    [Then(@"the verification report should have API version ""(.*)""")]
    public void ThenTheVerificationReportShouldHaveApiVersion(string expectedVersion)
    {
        Assert.IsNotNull(_verificationReport);
        Assert.AreEqual(expectedVersion, _verificationReport.ApiVersion);
    }
}
