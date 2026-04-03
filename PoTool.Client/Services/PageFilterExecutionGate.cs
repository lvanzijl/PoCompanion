using PoTool.Client.Models;

namespace PoTool.Client.Services;

public sealed class PageFilterExecutionGate
{
    public FilterExecutionGateResult Evaluate(FilterStateResolution? usage)
    {
        if (usage is null)
        {
            return new FilterExecutionGateResult(true, Array.Empty<string>(), Array.Empty<string>(), Array.Empty<string>());
        }

        var blockingMessages = usage.Status switch
        {
            FilterResolutionStatus.Invalid => usage.StateIssues.Count > 0
                ? usage.StateIssues
                : ["The selected filter state is invalid."],
            FilterResolutionStatus.Unresolved => BuildUnresolvedMessages(usage),
            _ => Array.Empty<string>()
        };

        var notAppliedMessages = BuildNotAppliedMessages(usage);
        var correctionMessages = usage.Status == FilterResolutionStatus.ResolvedWithNormalization
            ? usage.NormalizationDecisions
            : Array.Empty<string>();

        return new FilterExecutionGateResult(
            usage.Status is FilterResolutionStatus.Resolved or FilterResolutionStatus.ResolvedWithNormalization,
            blockingMessages,
            notAppliedMessages,
            correctionMessages);
    }

    public bool CanExecuteQueries(FilterStateResolution? usage)
        => Evaluate(usage).CanExecuteQueries;

    private static IReadOnlyList<string> BuildUnresolvedMessages(FilterStateResolution usage)
    {
        var messages = new List<string>();
        if (usage.MissingTeam)
        {
            messages.Add("Select a team to load this page.");
        }

        if (usage.MissingSprint)
        {
            messages.Add(usage.State.Time.Mode switch
            {
                FilterTimeMode.Range => "Select both a start sprint and an end sprint to load this page.",
                FilterTimeMode.Rolling => "Select a rolling window to load this page.",
                _ => "Select a sprint to load this page."
            });
        }

        return messages;
    }

    private static IReadOnlyList<string> BuildNotAppliedMessages(FilterStateResolution usage)
    {
        var messages = new List<string>();
        if (!usage.UsesProduct && !usage.State.AllProducts)
        {
            messages.Add("Product filter is active globally but not applied on this page.");
        }

        if (!usage.UsesProject && !usage.State.AllProjects)
        {
            messages.Add("Project filter is active globally but not applied on this page.");
        }

        if (!usage.UsesTeam && usage.State.TeamId.HasValue)
        {
            messages.Add("Team filter is active globally but not applied on this page.");
        }

        if (!usage.UsesTime && usage.State.Time.Mode != FilterTimeMode.Snapshot)
        {
            messages.Add("Time filter is active globally but not applied on this page.");
        }

        return messages;
    }
}

public sealed record FilterExecutionGateResult(
    bool CanExecuteQueries,
    IReadOnlyList<string> BlockingMessages,
    IReadOnlyList<string> NotAppliedMessages,
    IReadOnlyList<string> CorrectionMessages);
