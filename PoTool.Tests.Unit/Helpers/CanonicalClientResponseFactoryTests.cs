using Microsoft.VisualStudio.TestTools.UnitTesting;
using PoTool.Client.Helpers;
using PoTool.Shared.Metrics;
using PoTool.Shared.Pipelines;

namespace PoTool.Tests.Unit.Helpers;

[TestClass]
public sealed class CanonicalClientResponseFactoryTests
{
    [TestMethod]
    public void CreateNotice_WithNormalizedPipelineFilter_ReportsDifferenceAndInvalidField()
    {
        var response = new PipelineQueryResponseDto<string>
        {
            Data = "ok",
            RequestedFilter = new PipelineFilterContextDto
            {
                ProductIds = new FilterSelectionDto<int> { IsAll = false, Values = [42] },
                TeamIds = new FilterSelectionDto<int> { IsAll = true, Values = [] },
                RepositoryNames = new FilterSelectionDto<string> { IsAll = false, Values = ["Repo-A", "Repo-B"] },
                Time = new FilterTimeSelectionDto
                {
                    Mode = FilterTimeSelectionModeDto.DateRange,
                    SprintIds = [],
                    RangeStartUtc = new DateTimeOffset(2026, 3, 1, 0, 0, 0, TimeSpan.Zero),
                    RangeEndUtc = new DateTimeOffset(2026, 3, 31, 0, 0, 0, TimeSpan.Zero)
                }
            },
            EffectiveFilter = new PipelineFilterContextDto
            {
                ProductIds = new FilterSelectionDto<int> { IsAll = true, Values = [] },
                TeamIds = new FilterSelectionDto<int> { IsAll = true, Values = [] },
                RepositoryNames = new FilterSelectionDto<string> { IsAll = false, Values = ["Repo-A"] },
                Time = new FilterTimeSelectionDto
                {
                    Mode = FilterTimeSelectionModeDto.DateRange,
                    SprintIds = [],
                    RangeStartUtc = new DateTimeOffset(2026, 3, 1, 0, 0, 0, TimeSpan.Zero),
                    RangeEndUtc = new DateTimeOffset(2026, 3, 31, 0, 0, 0, TimeSpan.Zero)
                }
            },
            InvalidFields = ["productIds"],
            ValidationMessages =
            [
                new FilterValidationIssueDto
                {
                    Field = "productIds",
                    Message = "Requested product was outside the resolved scope."
                }
            ]
        };

        var notice = CanonicalClientResponseFactory.CreateNotice(
            CanonicalClientResponseFactory.Create(response).FilterMetadata);

        Assert.IsNotNull(notice);
        Assert.IsTrue(notice.HasSignals);
        Assert.IsTrue(notice.HasInvalidFields);
        Assert.IsTrue(notice.HasMaterialDifferences);
        Assert.AreEqual("Products", notice.ChangedDifferences[0].Label);
    }
}
