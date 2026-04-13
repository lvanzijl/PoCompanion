using Microsoft.VisualStudio.TestTools.UnitTesting;
using PoTool.Client.Helpers;
using PoTool.Shared.Metrics;

namespace PoTool.Tests.Unit.Helpers;

[TestClass]
public sealed class CanonicalClientResponseFactoryTests
{
    [TestMethod]
    public void CreateNotice_SprintMetadata_UsesReadableTeamAndSprintLabels()
    {
        var response = new SprintQueryResponseDto<string>
        {
            Data = "ok",
            RequestedFilter = new SprintFilterContextDto
            {
                ProductIds = new FilterSelectionDto<int> { IsAll = true, Values = Array.Empty<int>() },
                TeamIds = new FilterSelectionDto<int> { IsAll = false, Values = [7] },
                AreaPaths = new FilterSelectionDto<string> { IsAll = true, Values = Array.Empty<string>() },
                IterationPaths = new FilterSelectionDto<string> { IsAll = true, Values = Array.Empty<string>() },
                Time = new FilterTimeSelectionDto
                {
                    Mode = FilterTimeSelectionModeDto.Sprint,
                    SprintId = 701,
                    SprintIds = Array.Empty<int>()
                }
            },
            EffectiveFilter = new SprintFilterContextDto
            {
                ProductIds = new FilterSelectionDto<int> { IsAll = true, Values = Array.Empty<int>() },
                TeamIds = new FilterSelectionDto<int> { IsAll = false, Values = [7] },
                AreaPaths = new FilterSelectionDto<string> { IsAll = true, Values = Array.Empty<string>() },
                IterationPaths = new FilterSelectionDto<string> { IsAll = true, Values = Array.Empty<string>() },
                Time = new FilterTimeSelectionDto
                {
                    Mode = FilterTimeSelectionModeDto.Sprint,
                    SprintId = 702,
                    SprintIds = Array.Empty<int>()
                }
            },
            InvalidFields = Array.Empty<string>(),
            ValidationMessages = Array.Empty<FilterValidationIssueDto>(),
            TeamLabels = new Dictionary<int, string> { [7] = "Atlas" },
            SprintLabels = new Dictionary<int, string> { [701] = "Sprint Alpha", [702] = "Sprint Beta" }
        };

        var metadata = CanonicalClientResponseFactory.Create(response).FilterMetadata;
        var notice = CanonicalClientResponseFactory.CreateNotice(metadata);

        Assert.IsNotNull(notice);
        CollectionAssert.Contains(notice.ChangedDifferences.Select(difference => difference.RequestedValue).ToArray(), "Sprint Alpha");
        CollectionAssert.Contains(notice.ChangedDifferences.Select(difference => difference.EffectiveValue).ToArray(), "Sprint Beta");
    }
}
