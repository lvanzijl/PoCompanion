using PoTool.Shared.DataState;
using PoTool.Client.Models;

namespace PoTool.Client.Helpers;

public enum UiDataState
{
    NotRequested,
    Loading,
    Ready,
    NotReady,
    Failed,
    EmptyButValid,
    Invalid
}

public sealed record CacheStateDisplayContent(string Title, string Message);

public static class CacheStatePresentation
{
    public static UiDataState ToUiDataState(DataStateDto state)
        => state switch
        {
            DataStateDto.Loading => UiDataState.Loading,
            DataStateDto.Available => UiDataState.Ready,
            DataStateDto.NotReady => UiDataState.NotReady,
            DataStateDto.Failed => UiDataState.Failed,
            DataStateDto.Empty => UiDataState.EmptyButValid,
            _ => UiDataState.NotRequested
        };

    public static UiDataState ToUiDataState(DataStateResultStatus status)
        => status switch
        {
            DataStateResultStatus.Ready => UiDataState.Ready,
            DataStateResultStatus.Empty => UiDataState.EmptyButValid,
            DataStateResultStatus.NotReady => UiDataState.NotReady,
            DataStateResultStatus.Invalid => UiDataState.Invalid,
            DataStateResultStatus.Failed => UiDataState.Failed,
            DataStateResultStatus.Loading => UiDataState.Loading,
            _ => UiDataState.NotRequested
        };

    public static UiDataState ToUiDataState<T>(DataStateResult<T> result)
        => ToUiDataState(result.Status);

    public static CacheStateDisplayContent Create(string? subject, DataStateDto state, string? reason = null)
        => Create(subject, ToUiDataState(state), reason);

    public static CacheStateDisplayContent Create(string? subject, UiDataState state, string? reason = null)
    {
        var normalizedSubject = NormalizeSubject(subject);
        var normalizedReason = string.IsNullOrWhiteSpace(reason) ? null : reason.Trim();

        return state switch
        {
            UiDataState.NotReady => new CacheStateDisplayContent(
                "Data not ready",
                normalizedReason is null
                    ? $"{normalizedSubject} is waiting for cached data. Cache not built yet for this view."
                    : $"{normalizedReason} Cache not built yet for this view."),
            UiDataState.Invalid => new CacheStateDisplayContent(
                "Filter needs attention",
                normalizedReason ?? $"{normalizedSubject} could not be loaded for the current filter selection."),
            UiDataState.Failed => new CacheStateDisplayContent(
                "Data unavailable",
                normalizedReason ?? $"{normalizedSubject} could not be loaded right now."),
            UiDataState.EmptyButValid => new CacheStateDisplayContent(
                "No data available",
                normalizedReason ?? $"No {normalizedSubject.ToLowerInvariant()} is available for the current selection."),
            UiDataState.Loading => new CacheStateDisplayContent(
                "Loading",
                normalizedReason ?? $"{normalizedSubject} is loading."),
            UiDataState.Ready => new CacheStateDisplayContent(
                "Ready",
                normalizedReason ?? $"{normalizedSubject} is ready."),
            _ => new CacheStateDisplayContent(
                "Data not requested",
                normalizedReason ?? $"{normalizedSubject} has not been requested yet.")
        };
    }

    private static string NormalizeSubject(string? subject)
        => string.IsNullOrWhiteSpace(subject) ? "Data" : subject.Trim();
}
