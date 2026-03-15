using PoTool.Core.WorkItems.Filtering;
using PoTool.Shared.WorkItems;

namespace PoTool.Tests.Unit.Services;

[TestClass]
public sealed class WorkItemFiltererTests
{
    [TestMethod]
    public void GetWorkItemIdsByValidationFilter_UsesCanonicalRuleMetadata_ForMissingEffort()
    {
        var filterer = new WorkItemFilterer();
        var items = new[]
        {
            new FilterableWorkItem(1, null, new[] { new ValidationIssueAdapter("Warning", "Parent progress wording", "RC-2") }),
            new FilterableWorkItem(2, null, new[] { new ValidationIssueAdapter("Warning", "Effort wording", "RC-1") }),
        };

        var result = filterer.GetWorkItemIdsByValidationFilter(items, "missingEffort").ToList();

        CollectionAssert.AreEqual(new[] { 1 }, result);
    }

    [TestMethod]
    public void GetWorkItemIdsByValidationFilter_ExcludesRc2FromRefinementCompleteness()
    {
        var filterer = new WorkItemFilterer();
        var items = new[]
        {
            new FilterableWorkItem(1, null, new[] { new ValidationIssueAdapter("Warning", "Missing effort", "RC-2") }),
            new FilterableWorkItem(2, null, new[] { new ValidationIssueAdapter("Warning", "Missing description", "RC-1") }),
        };

        var result = filterer.GetWorkItemIdsByValidationFilter(items, "RefinementCompleteness").ToList();

        CollectionAssert.AreEqual(new[] { 2 }, result);
    }

    private sealed record FilterableWorkItem(
        int TfsId,
        int? ParentTfsId,
        IEnumerable<WorkItemFilterer.IValidationIssue> ValidationIssues) : WorkItemFilterer.IFilterableWorkItem;

    private sealed record ValidationIssueAdapter(
        string Severity,
        string Message,
        string? RuleId) : WorkItemFilterer.IValidationIssue;
}
