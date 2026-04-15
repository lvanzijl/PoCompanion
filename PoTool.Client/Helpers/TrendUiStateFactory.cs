using PoTool.Client.Models;
using PoTool.Client.Services;

namespace PoTool.Client.Helpers;

public static class TrendUiStateFactory
{
    public static DataStateViewModel<T> InvalidFilter<T>(FilterExecutionGateResult evaluation, string fallbackReason)
        => DataStateViewModel<T>.Invalid(GetBlockingReason(evaluation, fallbackReason));

    public static DataStateViewModel<T> InvalidFilter<T>(string reason)
        => DataStateViewModel<T>.Invalid(reason);

    public static string GetBlockingReason(FilterExecutionGateResult evaluation, string fallbackReason)
    {
        if (evaluation.BlockingMessages.Count == 0)
        {
            return fallbackReason;
        }

        return string.Join(" ", evaluation.BlockingMessages.Where(message => !string.IsNullOrWhiteSpace(message)));
    }
}
