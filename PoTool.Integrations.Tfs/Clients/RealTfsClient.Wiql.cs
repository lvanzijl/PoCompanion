using System.Text;
using System.Text.Json;
using PoTool.Shared.Settings;

namespace PoTool.Integrations.Tfs.Clients;

internal partial class RealTfsClient
{
    private string BuildAreaPathUnderClause(string areaPath)
    {
        var validatedAreaPath = ValidateRequiredWiqlLiteral(areaPath, "area path");
        return $"[System.AreaPath] UNDER '{EscapeWiql(validatedAreaPath)}'";
    }

    private string BuildWorkItemTypeEqualsClause(string workItemType)
    {
        var validatedWorkItemType = ValidateRequiredWiqlLiteral(workItemType, "work item type");
        return $"[System.WorkItemType] = '{EscapeWiql(validatedWorkItemType)}'";
    }

    private async Task<HttpResponseMessage> ExecuteWiqlQueryAsync(
        HttpClient httpClient,
        TfsConfigEntity config,
        string query,
        CancellationToken cancellationToken,
        bool handleErrors = false)
    {
        var validatedQuery = ValidateWiqlQuery(query);
        var wiqlUrl = ProjectUrl(config, "_apis/wit/wiql");
        using var wiqlContent = new StringContent(
            JsonSerializer.Serialize(new { query = validatedQuery }),
            Encoding.UTF8,
            "application/json");

        _logger.LogDebug("Executing WIQL query: {Query}", WiqlQueryBuilder.ToDiagnosticString(validatedQuery));
        return await SendPostAsync(httpClient, config, wiqlUrl, wiqlContent, cancellationToken, handleErrors);
    }

    private string ValidateWiqlQuery(string query)
    {
        try
        {
            WiqlQueryBuilder.Validate(query);
            return WiqlQueryBuilder.ToDiagnosticString(query);
        }
        catch (Exception ex) when (ex is InvalidOperationException or ArgumentException)
        {
            var diagnosticQuery = string.IsNullOrWhiteSpace(query)
                ? "<empty>"
                : WiqlQueryBuilder.ToDiagnosticString(query);

            _logger.LogError(ex, "Blocked malformed WIQL before TFS execution: {Query}", diagnosticQuery);
            throw new InvalidOperationException($"WIQL validation failed before execution: {ex.Message}", ex);
        }
    }

    private static string ValidateRequiredWiqlLiteral(string value, string parameterName)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new InvalidOperationException($"WIQL {parameterName} cannot be null or empty.");
        }

        return value.Trim();
    }
}
