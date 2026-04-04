using System.Net;
using PoTool.Client.ApiClient;

namespace PoTool.Client.Services;

internal static class GeneratedClientErrorTranslator
{
    internal static HttpRequestException ToHttpRequestException(ApiException exception)
    {
        ArgumentNullException.ThrowIfNull(exception);

        var statusCode = (HttpStatusCode)exception.StatusCode;
        return new HttpRequestException(
            $"Response status code does not indicate success: {exception.StatusCode} ({statusCode}).",
            exception,
            statusCode);
    }

    internal static bool IsSuccessfulEmptyResponse(ApiException exception)
    {
        ArgumentNullException.ThrowIfNull(exception);
        return exception.StatusCode is >= 200 and < 300
               && exception.Message.Contains("Response was null", StringComparison.Ordinal);
    }

    internal static bool IsSuccessfulDeserializationFailure(ApiException exception)
    {
        ArgumentNullException.ThrowIfNull(exception);
        return exception.StatusCode is >= 200 and < 300
               && exception.Message.Contains("Could not deserialize", StringComparison.Ordinal);
    }
}
