using PoTool.Shared.Metrics;
using PoTool.Client.Models;
using PoTool.Client.Services;

namespace PoTool.Client.Helpers;

public static class TrendUiStateFactory
{
    public static DataStateViewModel<T> InvalidFilter<T>(FilterExecutionGateResult evaluation, string fallbackReason)
        => DataStateViewModel<T>.Invalid(
            GetBlockingReason(evaluation, fallbackReason),
            data: default,
            invalidFields: evaluation.BlockingFields,
            validationMessages: ToValidationMessages(evaluation.BlockingMessages, evaluation.BlockingFields));

    public static DataStateViewModel<T> InvalidFilter<T>(
        string reason,
        IReadOnlyList<string>? invalidFields = null,
        IReadOnlyList<FilterValidationIssueDto>? validationMessages = null)
        => DataStateViewModel<T>.Invalid(reason, default, invalidFields, validationMessages);

    public static string GetBlockingReason(FilterExecutionGateResult evaluation, string fallbackReason)
    {
        if (evaluation.BlockingMessages.Count == 0)
        {
            return fallbackReason;
        }

        return string.Join(" ", evaluation.BlockingMessages.Where(message => !string.IsNullOrWhiteSpace(message)));
    }

    public static GlobalFilterValidationFeedback? CreateValidationFeedback<T>(DataStateViewModel<T> state)
    {
        if (state.UiState != UiDataState.Invalid)
        {
            return null;
        }

        var normalizedFields = GlobalFilterValidationMapper.NormalizeFields(state.InvalidFields);
        var validationMessages = state.ValidationMessages.Count == 0 && normalizedFields.Count == 0 && !string.IsNullOrWhiteSpace(state.Reason)
            ? [new FilterValidationIssueDto { Field = GlobalFilterValidationMapper.Time, Message = state.Reason }]
            : state.ValidationMessages;

        return new GlobalFilterValidationFeedback(state.Reason, normalizedFields, validationMessages);
    }

    private static IReadOnlyList<FilterValidationIssueDto> ToValidationMessages(
        IReadOnlyList<string> blockingMessages,
        IReadOnlyList<string> blockingFields)
    {
        if (blockingMessages.Count == 0)
        {
            return Array.Empty<FilterValidationIssueDto>();
        }

        var preferredField = blockingFields.FirstOrDefault() ?? GlobalFilterValidationMapper.Time;
        return blockingMessages
            .Where(message => !string.IsNullOrWhiteSpace(message))
            .Select(message => new FilterValidationIssueDto
            {
                Field = preferredField,
                Message = message
            })
            .ToArray();
    }
}
