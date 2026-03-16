using PoTool.Core.Contracts;
using PoTool.Core.Domain.BacklogQuality.Models;
using PoTool.Core.Domain.BacklogQuality.Services;
using PoTool.Shared.WorkItems;

namespace PoTool.Core.BacklogQuality;

public interface IBacklogQualityAnalysisService
{
    ValueTask<BacklogQualityAnalysisResult> AnalyzeAsync(
        IEnumerable<WorkItemDto> workItems,
        CancellationToken cancellationToken = default);
}

public sealed class BacklogQualityAnalysisService : IBacklogQualityAnalysisService
{
    private readonly IWorkItemStateClassificationService _stateClassificationService;
    private readonly BacklogQualityAnalyzer _backlogQualityAnalyzer;

    public BacklogQualityAnalysisService(
        IWorkItemStateClassificationService stateClassificationService,
        BacklogQualityAnalyzer backlogQualityAnalyzer)
    {
        _stateClassificationService = stateClassificationService ?? throw new ArgumentNullException(nameof(stateClassificationService));
        _backlogQualityAnalyzer = backlogQualityAnalyzer ?? throw new ArgumentNullException(nameof(backlogQualityAnalyzer));
    }

    public async ValueTask<BacklogQualityAnalysisResult> AnalyzeAsync(
        IEnumerable<WorkItemDto> workItems,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(workItems);

        var items = workItems as IReadOnlyList<WorkItemDto> ?? workItems.ToList();
        var response = await _stateClassificationService.GetClassificationsAsync(cancellationToken);
        var classifications = BacklogQualityDomainAdapter.CreateClassificationLookup(response.Classifications);
        var graph = BacklogQualityDomainAdapter.CreateGraph(
            items,
            item => BacklogQualityDomainAdapter.Classify(classifications, item));

        return _backlogQualityAnalyzer.Analyze(graph);
    }
}
