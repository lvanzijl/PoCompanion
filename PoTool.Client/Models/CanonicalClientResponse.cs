using PoTool.Shared.Metrics;

namespace PoTool.Client.Models;

public sealed record CanonicalClientResponse<TData>(
    TData Data,
    CanonicalFilterMetadata? FilterMetadata = null);

public enum CanonicalFilterKind
{
    PullRequest,
    Pipeline,
    Delivery,
    Sprint
}

public sealed record CanonicalFilterMetadata(
    CanonicalFilterKind Kind,
    object RequestedFilter,
    object EffectiveFilter,
    IReadOnlyList<string> InvalidFields,
    IReadOnlyList<FilterValidationIssueDto> ValidationMessages,
    IReadOnlyDictionary<int, string> TeamLabels,
    IReadOnlyDictionary<int, string> SprintLabels)
{
    public bool HasInvalidFields => InvalidFields.Count > 0;
}

public sealed record CanonicalFilterDisplayDifference(
    string Label,
    string RequestedValue,
    string EffectiveValue)
{
    public bool IsDifferent => !string.Equals(RequestedValue, EffectiveValue, StringComparison.Ordinal);
}

public sealed record CanonicalFilterNoticeModel(
    IReadOnlyList<CanonicalFilterDisplayDifference> Differences,
    IReadOnlyList<string> InvalidFields,
    IReadOnlyList<FilterValidationIssueDto> ValidationMessages)
{
    public bool HasInvalidFields => InvalidFields.Count > 0;

    public bool HasMaterialDifferences => Differences.Any(difference => difference.IsDifferent);

    public bool HasSignals => HasInvalidFields || HasMaterialDifferences || ValidationMessages.Count > 0;

    public IReadOnlyList<CanonicalFilterDisplayDifference> ChangedDifferences =>
        Differences.Where(difference => difference.IsDifferent).ToList();
}
