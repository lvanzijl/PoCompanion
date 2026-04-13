using System.Net;
using System.Net.Http;
using System.Linq;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Moq;
using PoTool.Core.Contracts;
using PoTool.Integrations.Tfs.Clients;
using PoTool.Shared.Settings;

namespace PoTool.Tests.Unit.Services;

[TestClass]
public class RealTfsClientRequestTests
{
    [TestMethod]
    public async Task GetAreaPathsAsync_UsesConfiguredTimeout()
    {
        var config = CreateConfig(timeoutSeconds: 0);
        var handler = new DelayedHttpMessageHandler(TimeSpan.FromSeconds(5));
        var client = CreateClient(config, handler);

        await AssertThrowsAsync<TimeoutException>(() => client.GetAreaPathsAsync());
    }

    [TestMethod]
    public async Task GetWorkItemsAsync_UsesUtcForChangedDateFilter()
    {
        var config = CreateConfig();
        string? capturedBody = null;

        var handler = new CaptureHttpMessageHandler(async (request, ct) =>
        {
            capturedBody = request.Content == null ? null : await request.Content.ReadAsStringAsync(ct);
            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent("{\"workItems\": []}")
            };
        });

        var client = CreateClient(config, handler);
        var since = new DateTimeOffset(2025, 1, 1, 10, 0, 0, TimeSpan.FromHours(2));

        var results = await client.GetWorkItemsAsync("TestProject\\Area", since);

        Assert.IsNotNull(capturedBody);
        using var doc = JsonDocument.Parse(capturedBody);
        var query = doc.RootElement.GetProperty("query").GetString();
        var expected = since.ToUniversalTime().ToString("O");
        StringAssert.Contains(query, expected);
        Assert.IsFalse(results.Any());
    }

    [TestMethod]
    public async Task GetWorkItemsByTypeAsync_UsesTypeFilteredWiqlAndBatchRequest()
    {
        var config = CreateConfig();
        var requestBodies = new List<string>();

        var handler = new CaptureHttpMessageHandler(async (request, ct) =>
        {
            if (request.Content != null)
            {
                requestBodies.Add(await request.Content.ReadAsStringAsync(ct));
            }

            if (request.RequestUri!.AbsoluteUri.Contains("_apis/wit/wiql", StringComparison.OrdinalIgnoreCase))
            {
                return new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = new StringContent("{\"workItems\":[{\"id\":101},{\"id\":102}]}")
                };
            }

            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent("{\"value\":[{\"id\":101,\"fields\":{\"System.Title\":\"Goal A\",\"System.WorkItemType\":\"goal\"}},{\"id\":102,\"fields\":{\"System.Title\":\"Goal B\",\"System.WorkItemType\":\"goal\"}}]}")
            };
        });

        var client = CreateClient(config, handler);

        var results = (await client.GetWorkItemsByTypeAsync("goal", "TestProject\\Area")).ToList();

        Assert.HasCount(2, requestBodies);

        using (var wiqlDoc = JsonDocument.Parse(requestBodies[0]))
        {
            var query = wiqlDoc.RootElement.GetProperty("query").GetString();
            StringAssert.Contains(query, "[System.WorkItemType] = 'goal'");
            StringAssert.Contains(query, "[System.AreaPath] UNDER 'TestProject\\Area'");
        }

        using (var batchDoc = JsonDocument.Parse(requestBodies[1]))
        {
            var fields = batchDoc.RootElement.GetProperty("fields").EnumerateArray().Select(element => element.GetString()).ToList();
            CollectionAssert.AreEquivalent(
                new[] { "System.Id", "System.Title", "System.WorkItemType" },
                fields.Where(value => value != null).Cast<string>().ToList());
        }

        Assert.AreEqual("Goal A", results[0].Title);
        Assert.AreEqual("Goal B", results[1].Title);
    }

    [TestMethod]
    public async Task GetWorkItemsAsync_RejectsEmptyAreaPathBeforeSendingWiql()
    {
        var config = CreateConfig();
        var requestCount = 0;

        var handler = new CaptureHttpMessageHandler((request, _) =>
        {
            requestCount++;
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent("{\"workItems\": []}")
            });
        });

        var client = CreateClient(config, handler);

        var ex = await AssertThrowsAsync<InvalidOperationException>(() => client.GetWorkItemsAsync("   "));

        StringAssert.Contains(ex.Message, "area path");
        Assert.AreEqual(0, requestCount);
    }

    [TestMethod]
    public async Task VerifyCapabilitiesAsync_WorkItemFieldValidationUsesHardenedWiqlWithoutTopClause()
    {
        var config = CreateConfig();
        var wiqlBodies = new List<string>();

        var handler = new CaptureHttpMessageHandler(async (request, ct) =>
        {
            var uri = request.RequestUri!.AbsoluteUri;
            if (request.Content != null && uri.Contains("_apis/wit/wiql", StringComparison.OrdinalIgnoreCase))
            {
                wiqlBodies.Add(await request.Content.ReadAsStringAsync(ct));
                return new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = new StringContent("{\"workItems\":[{\"id\":101}]}")
                };
            }

            if (uri.Contains("_apis/projects/", StringComparison.OrdinalIgnoreCase))
            {
                return new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = new StringContent("{\"name\":\"TestProject\"}")
                };
            }

            if (uri.Contains("_apis/projects", StringComparison.OrdinalIgnoreCase))
            {
                return new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = new StringContent("{\"value\":[{\"name\":\"TestProject\"}]}")
                };
            }

            if (uri.Contains("_apis/wit/fields", StringComparison.OrdinalIgnoreCase))
            {
                return new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = new StringContent("{\"value\":[" +
                        "{\"referenceName\":\"System.Id\"}," +
                        "{\"referenceName\":\"System.WorkItemType\"}," +
                        "{\"referenceName\":\"System.Title\"}," +
                        "{\"referenceName\":\"System.State\"}," +
                        "{\"referenceName\":\"System.AreaPath\"}," +
                        "{\"referenceName\":\"System.IterationPath\"}," +
                        "{\"referenceName\":\"System.Description\"}," +
                        "{\"referenceName\":\"System.CreatedDate\"}," +
                        "{\"referenceName\":\"System.ChangedDate\"}," +
                        "{\"referenceName\":\"Microsoft.VSTS.Common.ClosedDate\"}," +
                        "{\"referenceName\":\"Microsoft.VSTS.Common.Severity\"}," +
                        "{\"referenceName\":\"System.Tags\"}," +
                        "{\"referenceName\":\"Microsoft.VSTS.Common.BusinessValue\"}," +
                        "{\"referenceName\":\"Microsoft.VSTS.Scheduling.Effort\"}," +
                        "{\"referenceName\":\"Microsoft.VSTS.Scheduling.StoryPoints\"}," +
                        "{\"referenceName\":\"Microsoft.VSTS.Common.BacklogPriority\"}," +
                        "{\"referenceName\":\"Microsoft.VSTS.Common.TimeCriticality\"}," +
                        "{\"referenceName\":\"Rhodium.Funding.ProjectNumber\"}," +
                        "{\"referenceName\":\"Rhodium.Funding.ProjectElement\"}" +
                        "]}")
                };
            }

            if (uri.Contains("_apis/wit/workitemsbatch", StringComparison.OrdinalIgnoreCase))
            {
                return new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = new StringContent("{\"value\":[{\"id\":101,\"fields\":{" +
                        "\"System.WorkItemType\":\"Feature\"," +
                        "\"Rhodium.Funding.ProjectNumber\":\"PRJ-1\"," +
                        "\"Rhodium.Funding.ProjectElement\":\"ELM-1\"," +
                        "\"Microsoft.VSTS.Common.TimeCriticality\":42.5" +
                        "}}]}")
                };
            }

            if (uri.Contains("_apis/wit/workitems/1/revisions", StringComparison.OrdinalIgnoreCase))
            {
                return new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = new StringContent("{\"value\":[]}")
                };
            }

            if (uri.Contains("_apis/git/repositories", StringComparison.OrdinalIgnoreCase))
            {
                return new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = new StringContent("{\"value\":[]}")
                };
            }

            if (uri.Contains("_apis/build/definitions", StringComparison.OrdinalIgnoreCase) ||
                uri.Contains("_apis/release/definitions", StringComparison.OrdinalIgnoreCase))
            {
                return new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = new StringContent("{\"value\":[]}")
                };
            }

            return new HttpResponseMessage(HttpStatusCode.NotFound)
            {
                Content = new StringContent("{}")
            };
        });

        var client = CreateClient(config, handler);

        var result = await client.VerifyCapabilitiesAsync(includeWriteChecks: false);

        Assert.IsTrue(result.Success, "Verification should succeed for hardened WIQL regression coverage.");
        AssertCountAtLeast(wiqlBodies.Count, 3, "Expected at least three WIQL queries during capability verification.");
        Assert.IsTrue(wiqlBodies.All(body => !body.Contains("TOP", StringComparison.OrdinalIgnoreCase)));

        using var fieldValidationDoc = JsonDocument.Parse(wiqlBodies[^1]);
        var fieldValidationQuery = fieldValidationDoc.RootElement.GetProperty("query").GetString();
        Assert.AreEqual(
            "SELECT [System.Id] FROM WorkItems WHERE [System.TeamProject] = 'TestProject' AND [System.State] <> 'Removed' AND [System.AreaPath] UNDER 'TestProject\\Area' ORDER BY [System.ChangedDate] DESC",
            fieldValidationQuery);
    }

    [TestMethod]
    public async Task GetPullRequestsAsync_UsesUtcForMinAndMaxTime()
    {
        var config = CreateConfig();
        string? capturedQuery = null;

        var handler = new CaptureHttpMessageHandler((request, _) =>
        {
            var uri = request.RequestUri!.ToString();
            if (uri.Contains("_apis/git/repositories", StringComparison.OrdinalIgnoreCase) &&
                !uri.Contains("pullrequests", StringComparison.OrdinalIgnoreCase))
            {
                return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = new StringContent("{\"value\": [{\"name\": \"Repo1\", \"id\": \"1\"}]}")
                });
            }

            if (uri.Contains("pullrequests", StringComparison.OrdinalIgnoreCase))
            {
                capturedQuery = request.RequestUri!.Query;
                return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = new StringContent("{\"value\": []}")
                });
            }

            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.NotFound)
            {
                Content = new StringContent("{}")
            });
        });

        var client = CreateClient(config, handler);
        var from = new DateTimeOffset(2025, 1, 1, 10, 0, 0, TimeSpan.FromHours(2));
        var to = new DateTimeOffset(2025, 1, 1, 12, 0, 0, TimeSpan.FromHours(2));

        await client.GetPullRequestsAsync(null, from, to);

        Assert.IsNotNull(capturedQuery);
        var expectedMin = Uri.EscapeDataString(from.ToUniversalTime().ToString("O"));
        var expectedMax = Uri.EscapeDataString(to.ToUniversalTime().ToString("O"));
        StringAssert.Contains(capturedQuery, $"searchCriteria.minTime={expectedMin}");
        StringAssert.Contains(capturedQuery, $"searchCriteria.maxTime={expectedMax}");
    }

    [TestMethod]
    public async Task GetCoverageByBuildIdsAsync_UsesSupportedCoverageEndpointShapeWithoutBuildMetadataGate()
    {
        var config = CreateConfig();
        var requests = new List<Uri>();
        var logger = new Mock<ILogger<RealTfsClient>>();

        var handler = new CaptureHttpMessageHandler((request, _) =>
        {
            requests.Add(request.RequestUri!);
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent("{\"coverageData\":[]}")
            });
        });

        var client = CreateClient(config, handler, logger);

        var coverage = (await client.GetCoverageByBuildIdsAsync([101, 102])).ToList();

        Assert.IsFalse(coverage.Any());
        Assert.HasCount(2, requests);
        StringAssert.Contains(requests[0].AbsoluteUri, "_apis/testresults/codecoverage?buildId=101");
        StringAssert.Contains(requests[1].AbsoluteUri, "_apis/testresults/codecoverage?buildId=102");
        foreach (var request in requests)
        {
            StringAssert.Contains(request.Query, "api-version=7.0-preview");
            Assert.IsFalse(request.Query.Contains("flags=1", StringComparison.Ordinal));
            Assert.IsFalse(request.AbsoluteUri.Contains("_apis/build/builds", StringComparison.OrdinalIgnoreCase));
        }

        AssertLogged(logger, "BUILDQUALITY_COVERAGE_BUILD_SUMMARY: buildId=101");
        AssertLogged(logger, "BUILDQUALITY_COVERAGE_BUILD_SUMMARY: buildId=102");
        AssertLogged(logger, "Build 101 -> no coverage data.");
        AssertLogged(logger, "Build 102 -> no coverage data.");
        AssertLogged(logger, "BUILDQUALITY_COVERAGE_REQUEST_SUMMARY:");
    }

    [TestMethod]
    public async Task GetCoverageByBuildIdsAsync_UsesFirstValidLinesCoverageAndLogsMultipleEntries()
    {
        var config = CreateConfig();
        var logger = new Mock<ILogger<RealTfsClient>>();

        var handler = new CaptureHttpMessageHandler((request, _) =>
        {
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent("""
                    {
                      "coverageData": [
                        {
                          "coverageStats": [
                            { "label": "Branches", "covered": 1, "total": 2 },
                            { "label": "Lines", "covered": 40, "total": 50 }
                          ]
                        },
                        {
                          "coverageStats": [
                            { "label": "Lines", "covered": 90, "total": 100 }
                          ]
                        }
                      ]
                    }
                    """)
            });
        });

        var client = CreateClient(config, handler, logger);

        var coverage = (await client.GetCoverageByBuildIdsAsync([101])).ToList();

        Assert.HasCount(1, coverage);
        Assert.AreEqual(101, coverage[0].BuildId);
        Assert.AreEqual(40, coverage[0].CoveredLines);
        Assert.AreEqual(50, coverage[0].TotalLines);
        AssertLoggedAtLevel(logger, LogLevel.Warning, "Build 101 -> received multiple coverageData entries; attempting to use the first valid Lines stat only.");
        AssertLogged(logger, "Build 101 -> coverage retrieved (Covered=40, Total=50).");
        AssertLogged(logger, "BUILDQUALITY_COVERAGE_BUILD_SUMMARY: buildId=101");
    }

    [TestMethod]
    public async Task GetCoverageByBuildIdsAsync_LogsPerBuildWarningsForMalformedCoveragePayloads()
    {
        var config = CreateConfig();
        var logger = new Mock<ILogger<RealTfsClient>>();
        var requests = new List<Uri>();

        var handler = new CaptureHttpMessageHandler((request, _) =>
        {
            requests.Add(request.RequestUri!);
            var payload = request.RequestUri!.AbsoluteUri switch
            {
                var uri when uri.Contains("buildId=101", StringComparison.Ordinal) => """{"value":[]}""",
                var uri when uri.Contains("buildId=102", StringComparison.Ordinal) => """{"coverageData":[{}]}""",
                var uri when uri.Contains("buildId=103", StringComparison.Ordinal) => """{"coverageData":[{"coverageStats":[{"label":"Branches","covered":1,"total":2}]}]}""",
                var uri when uri.Contains("buildId=104", StringComparison.Ordinal) => """{"coverageData":[{"coverageStats":[{"label":"Lines","covered":4}]}]}""",
                var uri when uri.Contains("buildId=105", StringComparison.Ordinal) => """{"coverageData":[{"coverageStats":[{"label":"Lines","covered":9,"total":3}]}]}""",
                _ => """{"coverageData":[]}"""
            };

            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(payload)
            });
        });

        var client = CreateClient(config, handler, logger);

        var coverage = (await client.GetCoverageByBuildIdsAsync([101, 102, 103, 104, 105])).ToList();

        Assert.IsFalse(coverage.Any());
        Assert.HasCount(5, requests);
        AssertLoggedAtLevel(logger, LogLevel.Warning, "Build 101 -> no coverage data because the payload does not contain a coverageData array.");
        AssertLoggedAtLevel(logger, LogLevel.Warning, "Build 102 -> no coverage data because no coverageStats array was found.");
        AssertLoggedAtLevel(logger, LogLevel.Warning, "Build 103 -> no coverage data because no Lines coverage stat was found.");
        AssertLoggedAtLevel(logger, LogLevel.Warning, "Build 104 -> no coverage data because the Lines coverage stat is missing covered or total values.");
        AssertLoggedAtLevel(logger, LogLevel.Warning, "Build 105 -> no coverage data because the Lines coverage stat has invalid numeric values.");
        AssertLogged(logger, "Build 101 -> no coverage data.");
        AssertLogged(logger, "Build 102 -> no coverage data.");
        AssertLogged(logger, "Build 103 -> no coverage data.");
        AssertLogged(logger, "Build 104 -> no coverage data.");
        AssertLogged(logger, "Build 105 -> no coverage data.");
    }

    [TestMethod]
    public async Task GetTestRunsByBuildIdsAsync_UsesBuildUriQueryShapeWithSupportedApiVersion()
    {
        var config = CreateConfig();
        var requests = new List<Uri>();
        var logger = new Mock<ILogger<RealTfsClient>>();

        var handler = new CaptureHttpMessageHandler((request, _) =>
        {
            requests.Add(request.RequestUri!);

            var query = request.RequestUri!.Query;
            string payload;
            if (query.Contains("buildUri=vstfs%3A%2F%2F%2FBuild%2FBuild%2F101", StringComparison.Ordinal))
            {
                payload = "[{\"id\":5001,\"build\":{\"id\":999},\"buildUri\":\"vstfs:///Build/Build/999\",\"totalTests\":12,\"passedTests\":11,\"notApplicableTests\":1,\"completedDate\":\"2026-03-24T12:00:00Z\"}]";
            }
            else if (query.Contains("buildUri=vstfs%3A%2F%2F%2FBuild%2FBuild%2F102", StringComparison.Ordinal))
            {
                payload = "[]";
            }
            else
            {
                payload = "[]";
            }

            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(payload)
            });
        });

        var client = CreateClient(config, handler, logger);

        var testRuns = (await client.GetTestRunsByBuildIdsAsync([101, 102])).ToList();

        Assert.HasCount(1, testRuns);
        Assert.AreEqual(101, testRuns[0].BuildId);
        Assert.AreEqual(5001, testRuns[0].ExternalId);
        Assert.AreEqual(12, testRuns[0].TotalTests);
        Assert.AreEqual(11, testRuns[0].PassedTests);
        Assert.AreEqual(1, testRuns[0].NotApplicableTests);
        Assert.AreEqual(DateTimeOffset.Parse("2026-03-24T12:00:00Z"), testRuns[0].Timestamp);
        Assert.HasCount(2, requests);
        StringAssert.Contains(requests[0].AbsoluteUri, "_apis/test/runs?buildUri=vstfs%3A%2F%2F%2FBuild%2FBuild%2F101");
        StringAssert.Contains(requests[1].AbsoluteUri, "_apis/test/runs?buildUri=vstfs%3A%2F%2F%2FBuild%2FBuild%2F102");
        StringAssert.Contains(requests[0].Query, "api-version=7.0");
        foreach (var request in requests)
        {
            Assert.IsFalse(request.AbsoluteUri.Contains("_apis/build/builds", StringComparison.OrdinalIgnoreCase));
            Assert.IsFalse(request.Query.Contains("minLastUpdatedDate", StringComparison.Ordinal));
            Assert.IsFalse(request.Query.Contains("buildIds=", StringComparison.Ordinal));
            Assert.IsFalse(request.Query.Contains("$skip=", StringComparison.Ordinal));
            Assert.IsFalse(request.Query.Contains("$top=", StringComparison.Ordinal));
            Assert.AreEqual(1, request.Query.Split("buildUri=").Length - 1);
        }

        AssertLogged(logger, "BUILDQUALITY_TESTRUN_BUILD_SUMMARY: buildId=101");
        AssertLogged(logger, "BUILDQUALITY_TESTRUN_BUILD_SUMMARY: buildId=102");
        AssertLogged(logger, "Build 101 -> 1 aggregated run retrieved (Total=12, Passed=11, NotApplicable=1).");
        AssertLogged(logger, "Build 102 -> 0 aggregated runs retrieved.");
        AssertLogged(logger, "BUILDQUALITY_TESTRUN_REQUEST_SUMMARY:");
    }

    private static RealTfsClient CreateClient(
        TfsConfigEntity config,
        HttpMessageHandler handler,
        Mock<ILogger<RealTfsClient>>? logger = null)
    {
        var mockFactory = new Mock<IHttpClientFactory>();
        mockFactory.Setup(f => f.CreateClient(It.IsAny<string>()))
            .Returns(new HttpClient(handler));

        var configService = new Mock<ITfsConfigurationService>();
        configService.Setup(cs => cs.GetConfigEntityAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(config);

        var realLogger = logger ?? new Mock<ILogger<RealTfsClient>>();
        var throttlerLogger = new Mock<ILogger<TfsRequestThrottler>>();
        var throttler = new TfsRequestThrottler(throttlerLogger.Object);
        var senderLogger = new Mock<ILogger<TfsRequestSender>>();
        var requestSender = new TfsRequestSender(senderLogger.Object);

        return new RealTfsClient(
            mockFactory.Object,
            configService.Object,
            realLogger.Object,
            throttler,
            requestSender);
    }

    private static TfsConfigEntity CreateConfig(int timeoutSeconds = 30)
    {
        return new TfsConfigEntity
        {
            Url = "https://dev.azure.com/testorg",
            Project = "TestProject",
            ApiVersion = "7.0",
            TimeoutSeconds = timeoutSeconds,
            UseDefaultCredentials = true,
            DefaultAreaPath = "TestProject\\Area"
        };
    }

    private static async Task<TException> AssertThrowsAsync<TException>(Func<Task> action)
        where TException : Exception
    {
        try
        {
            await action();
        }
        catch (TException ex)
        {
            return ex;
        }

        Assert.Fail($"Expected exception of type {typeof(TException).Name}.");
        return null!;
    }

    private static void AssertCountAtLeast(int actualCount, int minimumCount, string message)
    {
        if (actualCount < minimumCount)
        {
            Assert.Fail(message);
        }
    }

    private sealed class DelayedHttpMessageHandler : HttpMessageHandler
    {
        private readonly TimeSpan _delay;

        public DelayedHttpMessageHandler(TimeSpan delay)
        {
            _delay = delay;
        }

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            await Task.Delay(_delay, cancellationToken);
            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent("{\"value\": []}")
            };
        }
    }

    private sealed class CaptureHttpMessageHandler : HttpMessageHandler
    {
        private readonly Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> _handler;

        public CaptureHttpMessageHandler(Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> handler)
        {
            _handler = handler;
        }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
            => _handler(request, cancellationToken);
    }

    private static void AssertLogged(Mock<ILogger<RealTfsClient>> logger, string messageFragment)
        => AssertLoggedAtLevel(logger, null, messageFragment);

    private static void AssertLoggedAtLevel(Mock<ILogger<RealTfsClient>> logger, LogLevel? level, string messageFragment)
    {
        if (level.HasValue)
        {
            logger.Verify(
                instance => instance.Log(
                    It.Is<LogLevel>(value => value == level.Value),
                    It.IsAny<EventId>(),
                    It.Is<It.IsAnyType>((state, _) => state.ToString()!.Contains(messageFragment, StringComparison.Ordinal)),
                    It.IsAny<Exception>(),
                    It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
                Times.AtLeastOnce);
            return;
        }

        logger.Verify(
            instance => instance.Log(
                It.IsAny<LogLevel>(),
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((state, _) => state.ToString()!.Contains(messageFragment, StringComparison.Ordinal)),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.AtLeastOnce);
    }
}
