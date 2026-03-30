using PoTool.Client.Models;
using PoTool.Shared.Metrics;
using PoTool.Shared.Pipelines;
using PoTool.Shared.PullRequests;

namespace PoTool.Client.Helpers;

public static class CanonicalClientResponseFactory
{
    public static CanonicalClientResponse<TData> Create<TData>(PullRequestQueryResponseDto<TData> response)
        => new(
            response.Data,
            new CanonicalFilterMetadata(
                CanonicalFilterKind.PullRequest,
                response.RequestedFilter,
                response.EffectiveFilter,
                response.InvalidFields,
                response.ValidationMessages));

    public static CanonicalClientResponse<TData> Create<TData>(PipelineQueryResponseDto<TData> response)
        => new(
            response.Data,
            new CanonicalFilterMetadata(
                CanonicalFilterKind.Pipeline,
                response.RequestedFilter,
                response.EffectiveFilter,
                response.InvalidFields,
                response.ValidationMessages));

    public static CanonicalClientResponse<TData> Create<TData>(DeliveryQueryResponseDto<TData> response)
        => new(
            response.Data,
            new CanonicalFilterMetadata(
                CanonicalFilterKind.Delivery,
                response.RequestedFilter,
                response.EffectiveFilter,
                response.InvalidFields,
                response.ValidationMessages));

    public static CanonicalClientResponse<TData> Create<TData>(SprintQueryResponseDto<TData> response)
        => new(
            response.Data,
            new CanonicalFilterMetadata(
                CanonicalFilterKind.Sprint,
                response.RequestedFilter,
                response.EffectiveFilter,
                response.InvalidFields,
                response.ValidationMessages));

    public static CanonicalFilterNoticeModel? CreateNotice(CanonicalFilterMetadata? metadata)
    {
        if (metadata is null)
        {
            return null;
        }

        var differences = metadata.Kind switch
        {
            CanonicalFilterKind.PullRequest => BuildPullRequestDifferences(metadata),
            CanonicalFilterKind.Pipeline => BuildPipelineDifferences(metadata),
            CanonicalFilterKind.Delivery => BuildDeliveryDifferences(metadata),
            CanonicalFilterKind.Sprint => BuildSprintDifferences(metadata),
            _ => Array.Empty<CanonicalFilterDisplayDifference>()
        };

        var notice = new CanonicalFilterNoticeModel(
            differences,
            metadata.InvalidFields,
            metadata.ValidationMessages);

        return notice.HasSignals ? notice : null;
    }

    private static IReadOnlyList<CanonicalFilterDisplayDifference> BuildPullRequestDifferences(CanonicalFilterMetadata metadata)
    {
        var requested = (PullRequestFilterContextDto)metadata.RequestedFilter;
        var effective = (PullRequestFilterContextDto)metadata.EffectiveFilter;

        return
        [
            CreateDifference("Products", FormatSelection(requested.ProductIds), FormatSelection(effective.ProductIds)),
            CreateDifference("Teams", FormatSelection(requested.TeamIds), FormatSelection(effective.TeamIds)),
            CreateDifference("Repositories", FormatSelection(requested.RepositoryNames), FormatSelection(effective.RepositoryNames)),
            CreateDifference("Iterations", FormatSelection(requested.IterationPaths), FormatSelection(effective.IterationPaths)),
            CreateDifference("Authors", FormatSelection(requested.CreatedBys), FormatSelection(effective.CreatedBys)),
            CreateDifference("Statuses", FormatSelection(requested.Statuses), FormatSelection(effective.Statuses)),
            CreateDifference("Time", FormatTime(requested.Time), FormatTime(effective.Time))
        ];
    }

    private static IReadOnlyList<CanonicalFilterDisplayDifference> BuildPipelineDifferences(CanonicalFilterMetadata metadata)
    {
        var requested = (PipelineFilterContextDto)metadata.RequestedFilter;
        var effective = (PipelineFilterContextDto)metadata.EffectiveFilter;

        return
        [
            CreateDifference("Products", FormatSelection(requested.ProductIds), FormatSelection(effective.ProductIds)),
            CreateDifference("Teams", FormatSelection(requested.TeamIds), FormatSelection(effective.TeamIds)),
            CreateDifference("Repositories", FormatSelection(requested.RepositoryIds), FormatSelection(effective.RepositoryIds)),
            CreateDifference("Time", FormatTime(requested.Time), FormatTime(effective.Time))
        ];
    }

    private static IReadOnlyList<CanonicalFilterDisplayDifference> BuildDeliveryDifferences(CanonicalFilterMetadata metadata)
    {
        var requested = (DeliveryFilterContextDto)metadata.RequestedFilter;
        var effective = (DeliveryFilterContextDto)metadata.EffectiveFilter;

        return
        [
            CreateDifference("Products", FormatSelection(requested.ProductIds), FormatSelection(effective.ProductIds)),
            CreateDifference("Time", FormatTime(requested.Time), FormatTime(effective.Time))
        ];
    }

    private static IReadOnlyList<CanonicalFilterDisplayDifference> BuildSprintDifferences(CanonicalFilterMetadata metadata)
    {
        var requested = (SprintFilterContextDto)metadata.RequestedFilter;
        var effective = (SprintFilterContextDto)metadata.EffectiveFilter;

        return
        [
            CreateDifference("Products", FormatSelection(requested.ProductIds), FormatSelection(effective.ProductIds)),
            CreateDifference("Teams", FormatSelection(requested.TeamIds), FormatSelection(effective.TeamIds)),
            CreateDifference("Areas", FormatSelection(requested.AreaPaths), FormatSelection(effective.AreaPaths)),
            CreateDifference("Iterations", FormatSelection(requested.IterationPaths), FormatSelection(effective.IterationPaths)),
            CreateDifference("Time", FormatTime(requested.Time), FormatTime(effective.Time))
        ];
    }

    private static CanonicalFilterDisplayDifference CreateDifference(string label, string requested, string effective)
        => new(label, requested, effective);

    private static string FormatSelection<T>(FilterSelectionDto<T> selection)
    {
        if (selection.IsAll)
        {
            return "All";
        }

        if (selection.Values.Count == 0)
        {
            return "None";
        }

        var formatted = selection.Values.Select(value => value?.ToString() ?? string.Empty).ToList();
        if (formatted.Count <= 3)
        {
            return string.Join(", ", formatted);
        }

        return $"{string.Join(", ", formatted.Take(3))} +{formatted.Count - 3} more";
    }

    private static string FormatTime(FilterTimeSelectionDto time)
    {
        return time.Mode switch
        {
            FilterTimeSelectionModeDto.None => "None",
            FilterTimeSelectionModeDto.CurrentSprint => "Current sprint",
            FilterTimeSelectionModeDto.Sprint => time.SprintId.HasValue ? $"Sprint #{time.SprintId.Value}" : "Sprint",
            FilterTimeSelectionModeDto.MultiSprint => time.SprintIds.Count switch
            {
                0 => "No sprints",
                <= 3 => $"Sprints {string.Join(", ", time.SprintIds)}",
                _ => $"{time.SprintIds.Count} sprints"
            },
            FilterTimeSelectionModeDto.DateRange => $"{FormatDate(time.RangeStartUtc)} → {FormatDate(time.RangeEndUtc)}",
            _ => time.Mode.ToString()
        };
    }

    private static string FormatDate(DateTimeOffset? value)
        => value.HasValue ? value.Value.ToString("yyyy-MM-dd") : "Open";
}
