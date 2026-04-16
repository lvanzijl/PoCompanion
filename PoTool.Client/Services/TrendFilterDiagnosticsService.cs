using System.Text.Json;
using Microsoft.AspNetCore.Components.WebAssembly.Hosting;
using PoTool.Client.Models;
using PoTool.Shared.Metrics;

namespace PoTool.Client.Services;

public sealed class TrendFilterDiagnosticsService
{
    private static readonly JsonSerializerOptions SerializerOptions = new(JsonSerializerDefaults.Web);
    private readonly IWebAssemblyHostEnvironment _hostEnvironment;
    private readonly ILogger<TrendFilterDiagnosticsService> _logger;

    public TrendFilterDiagnosticsService(
        IWebAssemblyHostEnvironment hostEnvironment,
        ILogger<TrendFilterDiagnosticsService> logger)
    {
        _hostEnvironment = hostEnvironment;
        _logger = logger;
    }

    public string? BuildSignature(
        string subject,
        CanonicalFilterMetadata? metadata,
        IReadOnlyList<string> invalidFields,
        IReadOnlyList<FilterValidationIssueDto> validationMessages,
        string? reason)
    {
        if (!_hostEnvironment.IsDevelopment() || metadata is null)
        {
            return null;
        }

        var requestedJson = JsonSerializer.Serialize(metadata.RequestedFilter, SerializerOptions);
        var effectiveJson = JsonSerializer.Serialize(metadata.EffectiveFilter, SerializerOptions);
        return string.Join(
            "|",
            subject,
            requestedJson,
            effectiveJson,
            string.Join(",", invalidFields),
            string.Join(";", validationMessages.Select(message => $"{message.Field}:{message.Message}")),
            reason ?? string.Empty);
    }

    public void LogIfNeeded(
        string subject,
        CanonicalFilterMetadata? metadata,
        IReadOnlyList<string> invalidFields,
        IReadOnlyList<FilterValidationIssueDto> validationMessages,
        string? reason)
    {
        if (!_hostEnvironment.IsDevelopment() || metadata is null)
        {
            return;
        }

        var requestedJson = JsonSerializer.Serialize(metadata.RequestedFilter, SerializerOptions);
        var effectiveJson = JsonSerializer.Serialize(metadata.EffectiveFilter, SerializerOptions);
        var hasMismatch = !string.Equals(requestedJson, effectiveJson, StringComparison.Ordinal);
        if (!hasMismatch && invalidFields.Count == 0 && validationMessages.Count == 0)
        {
            return;
        }

        _logger.LogWarning(
            "Trend filter diagnostics for {Subject}. RequestedFilter={RequestedFilter}; EffectiveFilter={EffectiveFilter}; InvalidFields={InvalidFields}; ValidationMessages={ValidationMessages}; Reason={Reason}",
            subject,
            requestedJson,
            effectiveJson,
            string.Join(", ", invalidFields),
            string.Join(" | ", validationMessages.Select(message => $"{message.Field}: {message.Message}")),
            reason ?? string.Empty);
    }
}
