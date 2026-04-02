using System.Collections.Specialized;

using PoTool.Client.Services;

namespace PoTool.Client.Helpers;

public static class RouteStateGuard
{
    public static bool TryGetRequiredQueryValue(
        NameValueCollection queryParameters,
        string key,
        out string value)
    {
        var rawValue = queryParameters[key];
        if (string.IsNullOrWhiteSpace(rawValue))
        {
            value = string.Empty;
            return false;
        }

        value = rawValue.Trim();
        return true;
    }

    public static ErrorResponse CreateMissingQueryParameterError(string parameterName, string recoveryGuidance)
        => new()
        {
            UserMessage = $"This page needs '{parameterName}' before it can open.",
            Suggestion = recoveryGuidance
        };

    public static ErrorResponse CreateInvalidQueryParameterError(string parameterName, string recoveryGuidance)
        => new()
        {
            UserMessage = $"'{parameterName}' is invalid for this page.",
            Suggestion = recoveryGuidance
        };

    public static ErrorResponse CreateMissingSelectionError(string subject, string recoveryGuidance)
        => new()
        {
            UserMessage = $"{subject} is required before this page can show data.",
            Suggestion = recoveryGuidance
        };

    public static ErrorResponse CreateNotFoundError(string subject, string recoveryGuidance)
        => new()
        {
            UserMessage = $"{subject} could not be found for the current context.",
            Suggestion = recoveryGuidance
        };

    public static ErrorResponse CreateUnavailableDataError(string subject, string recoveryGuidance)
        => new()
        {
            UserMessage = $"{subject} is not available for the current selection.",
            Suggestion = recoveryGuidance
        };

    public static ErrorResponse CreateSafeErrorResponse(
        ErrorMessageService errorMessageService,
        Exception exception,
        string context)
    {
        var response = errorMessageService.GetErrorResponse(exception, context);
        response.TechnicalDetails = new TechnicalErrorDetails
        {
            StatusCode = response.TechnicalDetails.StatusCode
        };

        return response;
    }
}
