using System.Net;
using System.Text;
using Microsoft.Extensions.Logging.Abstractions;
using PoTool.Client.ApiClient;
using PoTool.Client.Models;
using PoTool.Client.Services;
using PoTool.Shared.DataState;
using PoTool.Shared.Metrics;
using PoTool.Shared.Pipelines;
using PoTool.Shared.PullRequests;

namespace PoTool.Tests.Unit.Services;

[TestClass]
public sealed class GeneratedClientStateServiceTests
{
    [TestMethod]
    public async Task MetricsStateService_GetEffortDistributionStateAsync_MapsNotReadyEnvelope()
    {
        var service = new MetricsStateService(new MetricsClient(CreateHttpClient("""
            {
              "state": 4,
              "reason": "Cache warming"
            }
            """))
        {
            BaseUrl = "http://localhost"
        });

        var response = await service.GetEffortDistributionStateAsync(null, 5, null);

        Assert.IsNotNull(response);
        Assert.AreEqual(DataStateDto.NotReady, response.State);
        Assert.AreEqual("Cache warming", response.Reason);
    }

    [TestMethod]
    public async Task PipelineStateService_GetInsightsStateAsync_MapsFailedEnvelope()
    {
        var service = new PipelineStateService(new PipelinesClient(CreateHttpClient("""
            {
              "state": 5,
              "reason": "Pipeline cache failed"
            }
            """))
        {
            BaseUrl = "http://localhost"
        });

        var response = await service.GetInsightsStateAsync(7, 11, null, true, false);

        Assert.IsNotNull(response);
        Assert.AreEqual(DataStateDto.Failed, response.State);
        Assert.AreEqual("Pipeline cache failed", response.Reason);
    }

    [TestMethod]
    public async Task PullRequestStateService_GetDeliveryInsightsStateAsync_MapsEmptyEnvelope()
    {
        var service = new PullRequestStateService(new PullRequestsClient(CreateHttpClient("""
            {
              "state": 3,
              "reason": "No pull requests"
            }
            """))
        {
            BaseUrl = "http://localhost"
        });

        var response = await service.GetDeliveryInsightsStateAsync(3, 9, 4);

        Assert.IsNotNull(response);
        Assert.AreEqual(DataStateDto.Empty, response.State);
        Assert.AreEqual("No pull requests", response.Reason);
    }

    [TestMethod]
    public async Task WorkItemService_GetAllWithValidationStateAsync_MapsNotReadyEnvelope()
    {
        var httpClient = CreateHttpClient("""
            {
              "state": 4,
              "reason": "Validation cache building"
            }
            """);
        var client = new WorkItemsClient(httpClient)
        {
            BaseUrl = "http://localhost"
        };
        var service = new WorkItemService(client, httpClient, new WorkItemLoadCoordinatorService(NullLogger<WorkItemLoadCoordinatorService>.Instance));

        var response = await service.GetAllWithValidationStateAsync();

        Assert.IsNotNull(response);
        Assert.AreEqual(DataStateDto.NotReady, response.State);
        Assert.AreEqual("Validation cache building", response.Reason);
    }

    [TestMethod]
    public async Task ReleasePlanningService_GetBoardAsync_UsesGeneratedClientEnvelope()
    {
        var service = new ReleasePlanningService(
            new ReleasePlanningClient(CreateHttpClient("""
                {
                  "state": 4,
                  "reason": "Board cache not ready"
                }
                """))
            {
                BaseUrl = "http://localhost"
            },
            NullLogger<ReleasePlanningService>.Instance);

        var response = await service.GetBoardAsync();

        Assert.AreEqual(DataStateResultStatus.NotReady, response.Status);
        Assert.AreEqual("Board cache not ready", response.Reason);
    }

    [TestMethod]
    public async Task MetricsStateService_GetSprintExecutionStateAsync_DeserializesCanonicalLabelDictionaries()
    {
        var service = new MetricsStateService(new MetricsClient(CreateHttpClient("""
            {
              "state": 2,
              "data": {
                "data": {
                  "sprintId": 9,
                  "sprintName": "Sprint 11",
                  "summary": {
                    "committedSP": 21,
                    "addedSP": 0,
                    "removedSP": 0,
                    "deliveredSP": 0,
                    "deliveredFromAddedSP": 0,
                    "spilloverSP": 0,
                    "remainingStoryPoints": 21,
                    "churnRate": 0,
                    "commitmentCompletion": 0,
                    "spilloverRate": 0,
                    "addedDeliveryRate": 0,
                    "starvedCount": 0
                  },
                  "completedPbis": [],
                  "unfinishedPbis": [],
                  "addedDuringSprint": [],
                  "removedDuringSprint": [],
                  "spilloverPbis": [],
                  "starvedPbis": [],
                  "hasData": true
                },
                "requestedFilter": {
                  "productIds": { "isAll": false, "values": [1] },
                  "teamIds": { "isAll": false, "values": [4] },
                  "areaPaths": { "isAll": true, "values": [] },
                  "iterationPaths": { "isAll": true, "values": [] },
                  "time": { "mode": 1, "sprintId": 9, "sprintIds": [] }
                },
                "effectiveFilter": {
                  "productIds": { "isAll": false, "values": [1] },
                  "teamIds": { "isAll": false, "values": [4] },
                  "areaPaths": { "isAll": true, "values": [] },
                  "iterationPaths": { "isAll": false, "values": ["\\\\Battleship Systems\\\\Sprint 11"] },
                  "time": { "mode": 1, "sprintId": 9, "sprintIds": [] }
                },
                "invalidFields": [],
                "validationMessages": [],
                "teamLabels": {
                  "4": "Crew Safety"
                },
                "sprintLabels": {
                  "9": "Sprint 11"
                }
              }
            }
            """))
        {
            BaseUrl = "http://localhost"
        });

        var response = await service.GetSprintExecutionStateAsync(1, 9, 1);

        Assert.IsNotNull(response);
        Assert.AreEqual(DataStateDto.Available, response.State);
        Assert.IsNotNull(response.Data);
        Assert.AreEqual("Sprint 11", response.Data.Data!.SprintName);
        Assert.AreEqual("Crew Safety", response.Data.TeamLabels[4]);
        Assert.AreEqual("Sprint 11", response.Data.SprintLabels[9]);
    }

    private static HttpClient CreateHttpClient(string json)
        => new(new StubHttpMessageHandler(_ => new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent(json, Encoding.UTF8, "application/json")
        }))
        {
            BaseAddress = new Uri("http://localhost")
        };

    private sealed class StubHttpMessageHandler : HttpMessageHandler
    {
        private readonly Func<HttpRequestMessage, HttpResponseMessage> _handler;

        public StubHttpMessageHandler(Func<HttpRequestMessage, HttpResponseMessage> handler)
        {
            _handler = handler;
        }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
            => Task.FromResult(_handler(request));
    }
}
