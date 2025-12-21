using PoTool.Core.Exceptions;
using System.Net;

namespace PoTool.Client.Services;

/// <summary>
/// Service for converting technical exceptions into user-friendly error messages.
/// </summary>
public class ErrorMessageService
{
    /// <summary>
    /// Gets a user-friendly error response from an exception.
    /// </summary>
    /// <param name="exception">The exception to process.</param>
    /// <param name="context">Optional context about what operation failed.</param>
    /// <returns>A structured error response with user message and technical details.</returns>
    public ErrorResponse GetErrorResponse(Exception exception, string? context = null)
    {
        var response = new ErrorResponse
        {
            TechnicalDetails = new TechnicalErrorDetails
            {
                ExceptionType = exception.GetType().Name,
                ExceptionMessage = exception.Message,
                StackTrace = exception.StackTrace
            }
        };

        // Map specific TFS exceptions to user-friendly messages
        if (exception is TfsAuthenticationException authEx)
        {
            response.UserMessage = "Authentication failed. Please check your Personal Access Token and ensure it has not expired.";
            response.Suggestion = "Verify your PAT is correct and has the required permissions in Azure DevOps.";
            response.TechnicalDetails.StatusCode = authEx.StatusCode;
        }
        else if (exception is TfsAuthorizationException authzEx)
        {
            response.UserMessage = "Access denied. Please verify you have permission to access this resource.";
            response.Suggestion = "Check your project permissions in Azure DevOps or contact your administrator.";
            response.TechnicalDetails.StatusCode = authzEx.StatusCode;
        }
        else if (exception is TfsRateLimitException rateLimitEx)
        {
            response.UserMessage = "Too many requests. Please wait a moment before trying again.";
            response.Suggestion = "Azure DevOps has rate limits. Wait a few minutes and try again.";
            response.TechnicalDetails.StatusCode = rateLimitEx.StatusCode;
        }
        else if (exception is TfsResourceNotFoundException notFoundEx)
        {
            response.UserMessage = "Resource not found. Please verify your configuration.";
            response.Suggestion = "Check that your organization URL and project name are correct.";
            response.TechnicalDetails.StatusCode = notFoundEx.StatusCode;
        }
        else if (exception is TfsException tfsEx)
        {
            response.UserMessage = tfsEx.StatusCode.HasValue 
                ? MapHttpStatusToUserMessage(tfsEx.StatusCode.Value) 
                : "An error occurred while communicating with Azure DevOps.";
            response.Suggestion = tfsEx.StatusCode.HasValue 
                ? GetSuggestionForStatusCode(tfsEx.StatusCode.Value) 
                : "Please try again. If the problem persists, contact support.";
            response.TechnicalDetails.StatusCode = tfsEx.StatusCode;
        }
        else if (exception is HttpRequestException httpEx)
        {
            response.UserMessage = "Network error occurred. Please check your connection.";
            response.Suggestion = "Verify your internet connection and that the Azure DevOps URL is accessible.";
        }
        else if (exception is TaskCanceledException or OperationCanceledException)
        {
            response.UserMessage = "The operation timed out or was cancelled.";
            response.Suggestion = "Check your network connection or try increasing the timeout in settings.";
        }
        else
        {
            response.UserMessage = "An unexpected error occurred.";
            response.Suggestion = "Please try again. If the problem persists, contact support.";
        }

        // Add context if provided
        if (!string.IsNullOrEmpty(context))
        {
            response.UserMessage = $"{context}: {response.UserMessage}";
        }

        return response;
    }

    /// <summary>
    /// Maps HTTP status codes to user-friendly messages.
    /// </summary>
    private string MapHttpStatusToUserMessage(int statusCode)
    {
        return statusCode switch
        {
            400 => "Invalid request. Please check your input.",
            401 => "Authentication failed. Please check your Personal Access Token.",
            403 => "Access denied. Please verify you have permission to access this resource.",
            404 => "Resource not found. Please verify your configuration.",
            408 => "Request timed out. Please try again.",
            429 => "Too many requests. Please wait a moment before trying again.",
            500 => "Server error occurred. Please try again later.",
            502 => "Bad gateway. The server is temporarily unavailable.",
            503 => "Service unavailable. Please try again later.",
            504 => "Gateway timeout. The server took too long to respond.",
            _ when statusCode >= 500 => "Server error occurred. Please try again or contact support if the problem persists.",
            _ => "An error occurred while processing your request."
        };
    }

    /// <summary>
    /// Gets a helpful suggestion based on HTTP status code.
    /// </summary>
    private string GetSuggestionForStatusCode(int statusCode)
    {
        return statusCode switch
        {
            400 => "Review the entered values and ensure they are in the correct format.",
            401 => "Verify your Personal Access Token is correct and hasn't expired.",
            403 => "Check your project permissions in Azure DevOps or contact your administrator.",
            404 => "Verify your organization URL and project name are correct.",
            408 => "Check your network connection and try again.",
            429 => "Azure DevOps has rate limits. Wait a few minutes before retrying.",
            500 or 502 or 503 or 504 => "The issue is on the server side. Wait a few minutes and try again.",
            _ when statusCode >= 500 => "This appears to be a server issue. Contact support if it continues.",
            _ => "Try the operation again. Contact support if the problem persists."
        };
    }
}

/// <summary>
/// Structured error response with user-friendly message and technical details.
/// </summary>
public class ErrorResponse
{
    /// <summary>
    /// User-friendly error message (shown prominently).
    /// </summary>
    public string UserMessage { get; set; } = string.Empty;

    /// <summary>
    /// Suggestion for resolving the error (shown to user).
    /// </summary>
    public string? Suggestion { get; set; }

    /// <summary>
    /// Technical details (hidden behind "Show Details" by default).
    /// </summary>
    public TechnicalErrorDetails TechnicalDetails { get; set; } = new();
}

/// <summary>
/// Technical error details for debugging.
/// </summary>
public class TechnicalErrorDetails
{
    /// <summary>
    /// HTTP status code (if applicable).
    /// </summary>
    public int? StatusCode { get; set; }

    /// <summary>
    /// Exception type name.
    /// </summary>
    public string? ExceptionType { get; set; }

    /// <summary>
    /// Raw exception message.
    /// </summary>
    public string? ExceptionMessage { get; set; }

    /// <summary>
    /// Stack trace (for debugging).
    /// </summary>
    public string? StackTrace { get; set; }
}
