using PoTool.Core.Domain.Models;
using SharedStateClassification = PoTool.Shared.Settings.StateClassification;
using WorkItemStateClassificationDto = PoTool.Shared.Settings.WorkItemStateClassificationDto;

namespace PoTool.Api.Services;

internal static class StateClassificationInputMapper
{
    public static WorkItemStateClassification ToDomainStateClassification(this WorkItemStateClassificationDto dto)
    {
        return new WorkItemStateClassification(
            dto.WorkItemType,
            dto.StateName,
            dto.Classification.ToDomainStateClassification());
    }

    public static IReadOnlyList<WorkItemStateClassification> ToDomainStateClassifications(
        this IEnumerable<WorkItemStateClassificationDto> classifications)
    {
        return classifications.Select(classification => classification.ToDomainStateClassification()).ToList();
    }

    public static WorkItemStateClassificationDto ToDto(this WorkItemStateClassification classification)
    {
        return new WorkItemStateClassificationDto
        {
            WorkItemType = classification.WorkItemType,
            StateName = classification.StateName,
            Classification = classification.Classification.ToDtoStateClassification()
        };
    }

    private static StateClassification ToDomainStateClassification(this SharedStateClassification classification)
    {
        return classification switch
        {
            SharedStateClassification.New => StateClassification.New,
            SharedStateClassification.InProgress => StateClassification.InProgress,
            SharedStateClassification.Done => StateClassification.Done,
            SharedStateClassification.Removed => StateClassification.Removed,
            _ => throw new ArgumentOutOfRangeException(nameof(classification), classification, null)
        };
    }

    private static SharedStateClassification ToDtoStateClassification(this StateClassification classification)
    {
        return classification switch
        {
            StateClassification.New => SharedStateClassification.New,
            StateClassification.InProgress => SharedStateClassification.InProgress,
            StateClassification.Done => SharedStateClassification.Done,
            StateClassification.Removed => SharedStateClassification.Removed,
            _ => throw new ArgumentOutOfRangeException(nameof(classification), classification, null)
        };
    }
}
