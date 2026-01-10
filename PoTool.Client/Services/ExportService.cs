using System.Text;
using System.Text.Json;
using PoTool.Client.ApiClient;

namespace PoTool.Client.Services;

/// <summary>
/// Service for exporting work items to various formats (CSV, JSON).
/// </summary>
public class ExportService
{
    /// <summary>
    /// Exports work items to CSV format.
    /// </summary>
    /// <param name="workItems">The work items to export.</param>
    /// <returns>CSV content as a string.</returns>
    public string ExportToCsv(IEnumerable<WorkItemDto> workItems)
    {
        if (workItems == null || !workItems.Any())
            return string.Empty;

        var sb = new StringBuilder();

        // Header row
        sb.AppendLine("ID,Title,Type,State,Area Path,Iteration Path,Parent ID,Effort,Retrieved At");

        // Data rows
        foreach (var item in workItems)
        {
            sb.AppendLine($"{EscapeCsv(item.TfsId.ToString())}," +
                         $"{EscapeCsv(item.Title)}," +
                         $"{EscapeCsv(item.Type)}," +
                         $"{EscapeCsv(item.State)}," +
                         $"{EscapeCsv(item.AreaPath)}," +
                         $"{EscapeCsv(item.IterationPath)}," +
                         $"{EscapeCsv(item.ParentTfsId?.ToString() ?? string.Empty)}," +
                         $"{EscapeCsv(item.Effort?.ToString() ?? string.Empty)}," +
                         $"{EscapeCsv(item.RetrievedAt.ToString("yyyy-MM-dd HH:mm:ss"))}");
        }

        return sb.ToString();
    }

    /// <summary>
    /// Exports work items to JSON format.
    /// </summary>
    /// <param name="workItems">The work items to export.</param>
    /// <returns>JSON content as a string.</returns>
    public string ExportToJson(IEnumerable<WorkItemDto> workItems)
    {
        if (workItems == null)
            return "[]";

        var options = new JsonSerializerOptions
        {
            WriteIndented = true,
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase
        };

        return JsonSerializer.Serialize(workItems, options);
    }

    /// <summary>
    /// Escapes a CSV field value by wrapping it in quotes if necessary.
    /// </summary>
    private static string EscapeCsv(string? value)
    {
        if (string.IsNullOrEmpty(value))
            return string.Empty;

        // If the value contains comma, quote, or newline, wrap in quotes and escape internal quotes
        if (value.Contains(',') || value.Contains('"') || value.Contains('\n') || value.Contains('\r'))
        {
            return $"\"{value.Replace("\"", "\"\"")}\"";
        }

        return value;
    }
}
