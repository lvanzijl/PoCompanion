using PoTool.Client.Models;

namespace PoTool.Client.Helpers;

public static class CanonicalDataStateViewModelMapper
{
    public static DataStateViewModel<TPayload> Unwrap<TEnvelope, TPayload>(
        DataStateViewModel<TEnvelope> envelopeState,
        Func<TEnvelope, TPayload?> dataSelector,
        string emptyMessage,
        string? invalidMessage = null)
    {
        ArgumentNullException.ThrowIfNull(dataSelector);

        if (envelopeState.Data is { } envelope && dataSelector(envelope) is { } payload)
        {
            return envelopeState.UiState == UiDataState.Invalid
                ? DataStateViewModel<TPayload>.Invalid(
                    reason: envelopeState.Reason ?? invalidMessage,
                    data: payload,
                    invalidFields: envelopeState.InvalidFields,
                    validationMessages: envelopeState.ValidationMessages,
                    filterMetadata: envelopeState.FilterMetadata)
                : DataStateViewModel<TPayload>.Ready(payload) with
                {
                    FilterMetadata = envelopeState.FilterMetadata,
                    InvalidFields = envelopeState.InvalidFields,
                    ValidationMessages = envelopeState.ValidationMessages
                };
        }

        return envelopeState.UiState switch
        {
            UiDataState.Loading => DataStateViewModel<TPayload>.Loading(
                envelopeState.Reason,
                envelopeState.RetryAfterSeconds,
                envelopeState.ShowCacheStatus),
            UiDataState.Failed => DataStateViewModel<TPayload>.Failed(envelopeState.Reason),
            UiDataState.Invalid => DataStateViewModel<TPayload>.Invalid(envelopeState.Reason ?? invalidMessage),
            UiDataState.EmptyButValid => DataStateViewModel<TPayload>.Empty(envelopeState.Reason ?? emptyMessage),
            _ => DataStateViewModel<TPayload>.NotRequested()
        };
    }
}
