using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using PoTool.Client.Helpers;
using PoTool.Shared.Planning;

namespace PoTool.Client.Services;

public sealed class ProductPlanningBoardClientService
{
    private readonly HttpClient _httpClient;
    private readonly ILogger<ProductPlanningBoardClientService> _logger;

    public ProductPlanningBoardClientService(HttpClient httpClient, ILogger<ProductPlanningBoardClientService> logger)
    {
        _httpClient = httpClient;
        _logger = logger;
    }

    public Task<ProductPlanningBoardClientResult> GetBoardAsync(int productId, CancellationToken cancellationToken = default)
        => SendAsync<object?>(HttpMethod.Get, BuildBoardPath(productId), body: null, cancellationToken);

    public Task<ProductPlanningBoardClientResult> ResetAsync(int productId, CancellationToken cancellationToken = default)
        => SendAsync<object?>(HttpMethod.Post, $"{BuildBoardPath(productId)}/reset", body: null, cancellationToken);

    public Task<ProductPlanningBoardClientResult> MoveEpicBySprintsAsync(int productId, ProductPlanningEpicDeltaRequest request, CancellationToken cancellationToken = default)
        => SendAsync(HttpMethod.Post, $"{BuildBoardPath(productId)}/move", request, cancellationToken);

    public Task<ProductPlanningBoardClientResult> AdjustSpacingBeforeAsync(int productId, ProductPlanningEpicDeltaRequest request, CancellationToken cancellationToken = default)
        => SendAsync(HttpMethod.Post, $"{BuildBoardPath(productId)}/adjust-spacing", request, cancellationToken);

    public Task<ProductPlanningBoardClientResult> RunInParallelAsync(int productId, ProductPlanningEpicRequest request, CancellationToken cancellationToken = default)
        => SendAsync(HttpMethod.Post, $"{BuildBoardPath(productId)}/run-in-parallel", request, cancellationToken);

    public Task<ProductPlanningBoardClientResult> ReturnToMainAsync(int productId, ProductPlanningEpicRequest request, CancellationToken cancellationToken = default)
        => SendAsync(HttpMethod.Post, $"{BuildBoardPath(productId)}/return-to-main", request, cancellationToken);

    public Task<ProductPlanningBoardClientResult> ReorderEpicAsync(int productId, ReorderProductPlanningEpicRequest request, CancellationToken cancellationToken = default)
        => SendAsync(HttpMethod.Post, $"{BuildBoardPath(productId)}/reorder", request, cancellationToken);

    public Task<ProductPlanningBoardClientResult> ShiftPlanAsync(int productId, ProductPlanningEpicDeltaRequest request, CancellationToken cancellationToken = default)
        => SendAsync(HttpMethod.Post, $"{BuildBoardPath(productId)}/shift-plan", request, cancellationToken);

    public Task<ProductPlanningBoardClientResult> ReconcileProjectionAsync(int productId, ProductPlanningEpicRequest request, CancellationToken cancellationToken = default)
        => SendAsync(HttpMethod.Post, $"{BuildBoardPath(productId)}/reconcile", request, cancellationToken);

    private async Task<ProductPlanningBoardClientResult> SendAsync<TBody>(HttpMethod method, string requestUri, TBody? body, CancellationToken cancellationToken)
    {
        try
        {
            using var request = new HttpRequestMessage(method, requestUri);
            if (body is not null)
            {
                request.Content = JsonContent.Create(body, options: JsonHelper.CaseInsensitiveOptions);
            }

            using var response = await _httpClient.SendAsync(request, cancellationToken);
            if (response.StatusCode == HttpStatusCode.NotFound)
            {
                return ProductPlanningBoardClientResult.NotFound("The selected product planning board could not be found.");
            }

            if (!response.IsSuccessStatusCode)
            {
                var failure = await ReadFailureMessageAsync(response, cancellationToken);
                return ProductPlanningBoardClientResult.Failed(failure);
            }

            var board = await response.Content.ReadFromJsonAsync<ProductPlanningBoardDto>(JsonHelper.CaseInsensitiveOptions, cancellationToken);
            if (board is null)
            {
                return ProductPlanningBoardClientResult.Failed("The planning board endpoint returned an empty response.");
            }

            return ProductPlanningBoardClientResult.Success(board);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (HttpRequestException ex)
        {
            _logger.LogError(ex, "Planning board request {Method} {RequestUri} failed.", method, requestUri);
            return ProductPlanningBoardClientResult.Failed($"The planning board endpoint could not be reached: {ex.Message}");
        }
        catch (JsonException ex)
        {
            _logger.LogError(ex, "Planning board request {Method} {RequestUri} returned malformed JSON.", method, requestUri);
            return ProductPlanningBoardClientResult.Failed($"The planning board endpoint returned malformed JSON: {ex.Message}");
        }
        catch (NotSupportedException ex)
        {
            _logger.LogError(ex, "Planning board request {Method} {RequestUri} returned an unsupported payload.", method, requestUri);
            return ProductPlanningBoardClientResult.Failed($"The planning board endpoint returned an unsupported payload: {ex.Message}");
        }
    }

    private static async Task<string> ReadFailureMessageAsync(HttpResponseMessage response, CancellationToken cancellationToken)
    {
        try
        {
            var body = await response.Content.ReadAsStringAsync(cancellationToken);
            if (!string.IsNullOrWhiteSpace(body))
            {
                try
                {
                    using var json = JsonDocument.Parse(body);
                    if (json.RootElement.ValueKind == JsonValueKind.Object)
                    {
                        if (json.RootElement.TryGetProperty("message", out var message) && message.ValueKind == JsonValueKind.String)
                        {
                            return message.GetString() ?? body;
                        }

                        if (json.RootElement.TryGetProperty("detail", out var detail) && detail.ValueKind == JsonValueKind.String)
                        {
                            return detail.GetString() ?? body;
                        }

                        if (json.RootElement.TryGetProperty("title", out var title) && title.ValueKind == JsonValueKind.String)
                        {
                            return title.GetString() ?? body;
                        }
                    }
                }
                catch (JsonException)
                {
                }

                return $"The planning board endpoint returned HTTP {(int)response.StatusCode}: {body}";
            }
        }
        catch
        {
        }

        return $"The planning board endpoint returned HTTP {(int)response.StatusCode} ({response.StatusCode}).";
    }

    private static string BuildBoardPath(int productId) => $"/api/products/{productId}/planning-board";
}

public sealed record ProductPlanningBoardClientResult(
    ProductPlanningBoardDto? Board,
    bool IsNotFound,
    string? ErrorMessage)
{
    public bool IsSuccess => Board is not null && string.IsNullOrWhiteSpace(ErrorMessage) && !IsNotFound;

    public static ProductPlanningBoardClientResult Success(ProductPlanningBoardDto board) => new(board, false, null);

    public static ProductPlanningBoardClientResult NotFound(string message) => new(null, true, message);

    public static ProductPlanningBoardClientResult Failed(string message) => new(null, false, message);
}
