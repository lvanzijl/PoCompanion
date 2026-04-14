using PoTool.Client.Helpers;
using PoTool.Client.Models;
using PoTool.Client.ApiClient;
using PoTool.Shared.DataState;
using PoTool.Shared.Metrics;

namespace PoTool.Tests.Unit.Helpers;

[TestClass]
public sealed class GeneratedCacheEnvelopeHelperTests
{
    [TestMethod]
    public void ToDataStateResponse_ConvertsGeneratedLikeSprintEnvelope_ToSharedResponse()
    {
        var envelope = new TestEnvelope<TestGeneratedSprintQueryResponse>
        {
            State = DataStateDto.Available,
            Data = new TestGeneratedSprintQueryResponse
            {
                Data = 42,
                RequestedFilter = CreateSprintFilter(),
                EffectiveFilter = CreateSprintFilter(),
                InvalidFields = Array.Empty<string>(),
                ValidationMessages = Array.Empty<FilterValidationIssueDto>(),
                TeamLabels = new Dictionary<int, string>(),
                SprintLabels = new Dictionary<int, string>()
            },
            Reason = "ready",
            RetryAfterSeconds = (int?)null
        };

        var response = GeneratedCacheEnvelopeHelper.ToDataStateResponse(
            envelope,
            static data => new SprintQueryResponseDto<int>
            {
                Data = data.Data,
                RequestedFilter = data.RequestedFilter,
                EffectiveFilter = data.EffectiveFilter,
                InvalidFields = data.InvalidFields,
                ValidationMessages = data.ValidationMessages,
                TeamLabels = data.TeamLabels,
                SprintLabels = data.SprintLabels
            });

        Assert.AreEqual(DataStateDto.Available, response.State);
        Assert.IsNotNull(response.Data);
        Assert.AreEqual(42, response.Data.Data);
        Assert.AreEqual(FilterTimeSelectionModeDto.None, response.Data.RequestedFilter.Time.Mode);
    }

    [TestMethod]
    public void ToCacheBackedResult_MapsEnvelopeStates()
    {
        var cases = new[]
        {
            (DataState: DataStateDto.Available, ExpectedState: CacheBackedClientState.Success),
            (DataState: DataStateDto.Empty, ExpectedState: CacheBackedClientState.Empty),
            (DataState: DataStateDto.NotReady, ExpectedState: CacheBackedClientState.NotReady),
            (DataState: DataStateDto.Failed, ExpectedState: CacheBackedClientState.Failed)
        };

        foreach (var testCase in cases)
        {
            var envelope = new TestEnvelope<StatePayload>
            {
                State = testCase.DataState,
                Data = testCase.DataState == DataStateDto.Available ? new StatePayload { Value = 7 } : null,
                Reason = "state-reason",
                RetryAfterSeconds = 15
            };

            var result = GeneratedCacheEnvelopeHelper.ToCacheBackedResult<StatePayload>(envelope);

            Assert.AreEqual(testCase.ExpectedState, result.State);
            Assert.AreEqual(
                testCase.DataState == DataStateDto.Available ? null : "state-reason",
                result.Reason);
            Assert.AreEqual(testCase.DataState == DataStateDto.Available ? null : 15, result.RetryAfterSeconds);
            Assert.AreEqual(testCase.DataState == DataStateDto.Available ? 7 : default(int?), result.Data?.Value);
        }
    }

    private static SprintFilterContextDto CreateSprintFilter()
        => new()
        {
            ProductIds = new FilterSelectionDto<int> { IsAll = true, Values = Array.Empty<int>() },
            TeamIds = new FilterSelectionDto<int> { IsAll = true, Values = Array.Empty<int>() },
            AreaPaths = new FilterSelectionDto<string> { IsAll = true, Values = Array.Empty<string>() },
            IterationPaths = new FilterSelectionDto<string> { IsAll = true, Values = Array.Empty<string>() },
            Time = new FilterTimeSelectionDto
            {
                Mode = FilterTimeSelectionModeDto.None,
                SprintIds = Array.Empty<int>()
            }
        };

    private sealed record StatePayload
    {
        public int Value { get; init; }
    }

    private sealed record TestGeneratedSprintQueryResponse
    {
        public int Data { get; init; }
        public required SprintFilterContextDto RequestedFilter { get; init; }
        public required SprintFilterContextDto EffectiveFilter { get; init; }
        public required IReadOnlyList<string> InvalidFields { get; init; }
        public required IReadOnlyList<FilterValidationIssueDto> ValidationMessages { get; init; }
        public required IReadOnlyDictionary<int, string> TeamLabels { get; init; }
        public required IReadOnlyDictionary<int, string> SprintLabels { get; init; }
    }

    private sealed record TestEnvelope<T> : IGeneratedDataStateEnvelope<T>
    {
        public DataStateDto State { get; init; }
        public T? Data { get; init; }
        public string? Reason { get; init; }
        public int? RetryAfterSeconds { get; init; }
    }
}
