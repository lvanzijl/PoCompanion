using PoTool.Core.Metrics.Models;
using PoTool.Shared.Settings;

namespace PoTool.Api.Services;

internal static class StateClassificationLookup
{
    private static readonly Lazy<IReadOnlyDictionary<(string WorkItemType, string StateName), StateClassification>> DefaultLookup =
        new(() => Create(WorkItemStateClassificationService.GetDefaultClassifications()));

    public static IReadOnlyDictionary<(string WorkItemType, string StateName), StateClassification> Default => DefaultLookup.Value;

    public static IReadOnlyDictionary<(string WorkItemType, string StateName), StateClassification> Create(
        IEnumerable<WorkItemStateClassificationDto> classifications)
    {
        var groupedClassifications = classifications
            .GroupBy(
                classification => (classification.WorkItemType, classification.StateName),
                StateKeyComparer.Instance)
            .ToList();

        var duplicateKey = groupedClassifications.FirstOrDefault(group => group.Skip(1).Any());
        if (duplicateKey != null)
        {
            throw new InvalidOperationException(
                $"Duplicate state classification detected for work item type '{duplicateKey.Key.WorkItemType}' and state '{duplicateKey.Key.StateName}'.");
        }

        return groupedClassifications.ToDictionary(
            group => group.Key,
            group => group.Single().Classification,
            StateKeyComparer.Instance);
    }

    public static StateClassification GetClassification(
        IReadOnlyDictionary<(string WorkItemType, string StateName), StateClassification>? lookup,
        string workItemType,
        string? state)
    {
        if (string.IsNullOrWhiteSpace(state))
        {
            return StateClassification.New;
        }

        var resolvedLookup = lookup ?? Default;
        return resolvedLookup.TryGetValue((workItemType, state), out var classification)
            ? classification
            : StateClassification.New;
    }

    public static bool IsDone(
        IReadOnlyDictionary<(string WorkItemType, string StateName), StateClassification>? lookup,
        string workItemType,
        string? state)
    {
        return GetClassification(lookup, workItemType, state) == StateClassification.Done;
    }

    public static bool IsDone(
        IReadOnlyDictionary<(string WorkItemType, string StateName), StateClassification>? lookup,
        WorkItemSnapshot workItem)
    {
        return IsDone(lookup, workItem.WorkItemType, workItem.CurrentState);
    }

    public static IReadOnlySet<string> GetStatesForClassification(
        IReadOnlyDictionary<(string WorkItemType, string StateName), StateClassification>? lookup,
        string workItemType,
        StateClassification classification)
    {
        var resolvedLookup = lookup ?? Default;
        return resolvedLookup
            .Where(pair => string.Equals(pair.Key.WorkItemType, workItemType, StringComparison.OrdinalIgnoreCase)
                && pair.Value == classification)
            .Select(pair => pair.Key.StateName)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
    }

    private sealed class StateKeyComparer : IEqualityComparer<(string WorkItemType, string StateName)>
    {
        public static StateKeyComparer Instance { get; } = new();

        public bool Equals((string WorkItemType, string StateName) x, (string WorkItemType, string StateName) y)
        {
            return string.Equals(x.WorkItemType, y.WorkItemType, StringComparison.OrdinalIgnoreCase)
                && string.Equals(x.StateName, y.StateName, StringComparison.OrdinalIgnoreCase);
        }

        public int GetHashCode((string WorkItemType, string StateName) obj)
        {
            return HashCode.Combine(
                StringComparer.OrdinalIgnoreCase.GetHashCode(obj.WorkItemType),
                StringComparer.OrdinalIgnoreCase.GetHashCode(obj.StateName));
        }
    }
}
