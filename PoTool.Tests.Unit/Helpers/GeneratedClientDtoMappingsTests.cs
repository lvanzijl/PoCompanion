using PoTool.Client.ApiClient;
using PoTool.Client.Helpers;
using PoTool.Shared.Metrics;
using PoTool.Shared.Pipelines;
using PoTool.Shared.PullRequests;

namespace PoTool.Tests.Unit.Helpers;

[TestClass]
public sealed class GeneratedClientDtoMappingsTests
{
    [TestMethod]
    public void DeliveryQueryMapping_PreservesFilterMetadataAndPayload()
    {
        var response = new DeliveryQueryResponseDtoOfPortfolioDeliveryDto
        {
            Data = new PortfolioDeliveryDto
            {
                Summary = new PortfolioDeliverySummaryDto(),
                Products = Array.Empty<ProductDeliveryDto>(),
                TopFeatures = Array.Empty<FeatureDeliveryDto>()
            },
            RequestedFilter = CreateDeliveryFilter(),
            EffectiveFilter = CreateDeliveryFilter(),
            InvalidFields = ["productIds"],
            ValidationMessages =
            [
                new FilterValidationIssueDto
                {
                    Field = "productIds",
                    Message = "Invalid product"
                }
            ]
        };

        var mapped = response.ToShared();

        Assert.AreSame(response.Data, mapped.Data);
        Assert.AreEqual(FilterTimeSelectionModeDto.MultiSprint, mapped.RequestedFilter.Time.Mode);
        CollectionAssert.AreEqual(new[] { "productIds" }, mapped.InvalidFields.ToArray());
        Assert.AreEqual("Invalid product", mapped.ValidationMessages.Single().Message);
    }

    [TestMethod]
    public void PullRequestCollectionMapping_MaterializesReadOnlyList()
    {
        var response = new PullRequestQueryResponseDtoOfIReadOnlyListOfPullRequestDto
        {
            Data =
            [
                new PullRequestDto(1, "Repo", "One", "Alice", DateTimeOffset.UtcNow, null, "Open", "Sprint 1", "feature/a", "main", DateTimeOffset.UtcNow),
                new PullRequestDto(2, "Repo", "Two", "Bob", DateTimeOffset.UtcNow, null, "Open", "Sprint 1", "feature/b", "main", DateTimeOffset.UtcNow)
            ],
            RequestedFilter = CreatePullRequestFilter(),
            EffectiveFilter = CreatePullRequestFilter(),
            InvalidFields = Array.Empty<string>(),
            ValidationMessages = Array.Empty<FilterValidationIssueDto>()
        };

        var mapped = response.ToShared();

        Assert.HasCount(2, mapped.Data);
        Assert.AreEqual("One", mapped.Data[0].Title);
    }

    [TestMethod]
    public void PipelineCollectionMapping_MaterializesReadOnlyList()
    {
        var response = new PipelineQueryResponseDtoOfIReadOnlyListOfPipelineMetricsDto
        {
            Data =
            [
                new PipelineMetricsDto(7, "Build", PipelineType.Build, 1, 1, 0, 0, 0, 0, null, null, null, null, PipelineRunResult.Succeeded, null, 0)
            ],
            RequestedFilter = CreatePipelineFilter(),
            EffectiveFilter = CreatePipelineFilter(),
            InvalidFields = Array.Empty<string>(),
            ValidationMessages = Array.Empty<FilterValidationIssueDto>()
        };

        var mapped = response.ToShared();

        Assert.HasCount(1, mapped.Data);
        Assert.AreEqual(7, mapped.Data[0].PipelineId);
    }

    private static DeliveryFilterContextDto CreateDeliveryFilter()
        => new()
        {
            ProductIds = new FilterSelectionDto<int> { IsAll = false, Values = [42] },
            Time = new FilterTimeSelectionDto
            {
                Mode = FilterTimeSelectionModeDto.MultiSprint,
                SprintIds = [5, 6]
            }
        };

    private static PipelineFilterContextDto CreatePipelineFilter()
        => new()
        {
            ProductIds = new FilterSelectionDto<int> { IsAll = true, Values = Array.Empty<int>() },
            TeamIds = new FilterSelectionDto<int> { IsAll = true, Values = Array.Empty<int>() },
            RepositoryIds = new FilterSelectionDto<int> { IsAll = true, Values = Array.Empty<int>() },
            Time = new FilterTimeSelectionDto
            {
                Mode = FilterTimeSelectionModeDto.None,
                SprintIds = Array.Empty<int>()
            }
        };

    private static PullRequestFilterContextDto CreatePullRequestFilter()
        => new()
        {
            ProductIds = new FilterSelectionDto<int> { IsAll = true, Values = Array.Empty<int>() },
            TeamIds = new FilterSelectionDto<int> { IsAll = true, Values = Array.Empty<int>() },
            RepositoryNames = new FilterSelectionDto<string> { IsAll = true, Values = Array.Empty<string>() },
            IterationPaths = new FilterSelectionDto<string> { IsAll = true, Values = Array.Empty<string>() },
            CreatedBys = new FilterSelectionDto<string> { IsAll = true, Values = Array.Empty<string>() },
            Statuses = new FilterSelectionDto<string> { IsAll = true, Values = Array.Empty<string>() },
            Time = new FilterTimeSelectionDto
            {
                Mode = FilterTimeSelectionModeDto.None,
                SprintIds = Array.Empty<int>()
            }
        };
}
