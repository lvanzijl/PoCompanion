using PoTool.Shared.Metrics;

namespace PoTool.Client.Models;

public sealed record GlobalFilterValidationFeedback(
    string? SummaryMessage,
    IReadOnlyList<string> InvalidFields,
    IReadOnlyList<FilterValidationIssueDto> ValidationMessages)
{
    public bool HasFeedback => !string.IsNullOrWhiteSpace(SummaryMessage)
        || InvalidFields.Count > 0
        || ValidationMessages.Count > 0;
}
