using System.Net;
using System.Net.Http;
using System.Text.Json;
using PoTool.Client.ApiClient;
using PoTool.Shared.DataState;

namespace PoTool.Client.Helpers;

internal static class ApiErrorMessageFormatter
{
    public static string ForSprintActivity(ApiException exception)
    {
        if (exception.StatusCode == 404)
        {
            return "No activity details are available for this work item.";
        }

        if (exception.StatusCode == 409 && HasCacheNotReadyProblem(exception.Response))
        {
            return "Sprint activity is unavailable until cached data is ready. Run a successful sync for the active profile and try again.";
        }

        return "Sprint activity could not be loaded right now. Try again later.";
    }

    public static string ForBugsTriage(ApiException exception)
    {
        if (exception.StatusCode == 409 && HasCacheNotReadyProblem(exception.Response))
        {
            return "Bugs triage is unavailable until cached data is ready. Run a successful sync for the active profile and try again.";
        }

        return "Bugs triage could not be loaded right now. Try again later.";
    }

    public static string ForBugDetail(ApiException exception)
    {
        if (exception.StatusCode == 404)
        {
            return "That bug could not be found. Open Bug Insights to pick a valid bug.";
        }

        if (exception.StatusCode == 409 && HasCacheNotReadyProblem(exception.Response))
        {
            return "Bug details are unavailable until cached data is ready. Run a successful sync for the active profile and try again.";
        }

        return "Bug details could not be loaded right now. Try again later.";
    }

    public static string ForPortfolioFlow(ApiException exception)
    {
        if (exception.StatusCode == 409 && HasCacheNotReadyProblem(exception.Response))
        {
            return "Portfolio flow is unavailable until cached data is ready. Run a successful sync for the active profile and try again.";
        }

        return "Portfolio flow could not be loaded right now. Try again later.";
    }

    public static string ForPortfolioHistory(ApiException exception)
    {
        if (exception.StatusCode == 409 && HasCacheNotReadyProblem(exception.Response))
        {
            return "Portfolio history is unavailable until cached data is ready. Run a successful sync for the active profile and try again.";
        }

        return "Portfolio history could not be loaded right now. Try again later.";
    }

    public static string ForValidationQueue(HttpRequestException exception)
        => exception.StatusCode == HttpStatusCode.Conflict
            ? "Validation queue data is unavailable until cached data is ready. Run a successful sync for the active profile and try again."
            : "Validation queue data could not be loaded right now. Try again later.";

    public static string ForValidationFix(HttpRequestException exception)
        => exception.StatusCode == HttpStatusCode.Conflict
            ? "Validation fix data is unavailable until cached data is ready. Run a successful sync for the active profile and try again."
            : "Validation fix data could not be loaded right now. Try again later.";

    private static bool HasCacheNotReadyProblem(string? responseBody)
    {
        if (string.IsNullOrWhiteSpace(responseBody))
        {
            return false;
        }

        try
        {
            using var document = JsonDocument.Parse(responseBody);
            if (!document.RootElement.TryGetProperty("title", out var titleElement))
            {
                return false;
            }

            var title = titleElement.GetString();
            return string.Equals(title, DataStateContract.CacheNotReadyTitle, StringComparison.OrdinalIgnoreCase);
        }
        catch (JsonException)
        {
            return false;
        }
    }
}
