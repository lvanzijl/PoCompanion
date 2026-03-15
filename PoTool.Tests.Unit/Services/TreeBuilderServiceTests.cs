using PoTool.Client.Services;
using PoTool.Shared.WorkItems;
using ClientValidationIssue = PoTool.Client.ApiClient.ValidationIssue;
using ClientWorkItemWithValidationDto = PoTool.Client.ApiClient.WorkItemWithValidationDto;

namespace PoTool.Tests.Unit.Services;

[TestClass]
public sealed class TreeBuilderServiceTests
{
    [TestMethod]
    public void BuildTreeWithValidation_UsesCanonicalMetadata_ForHighestCategory()
    {
        var service = new TreeBuilderService();
        var items = new[]
        {
            new ClientWorkItemWithValidationDto
            {
                TfsId = 1,
                Title = "Feature",
                Type = "Feature",
                State = "New",
                ValidationIssues = new List<ClientValidationIssue>
                {
                    new()
                    {
                        Severity = "Warning",
                        Message = "Looks like a parent-progress issue",
                        RuleId = "RC-2"
                    }
                }
            }
        };

        var result = service.BuildTreeWithValidation(items, new Dictionary<int, bool>());

        Assert.AreEqual(ValidationCategory.MissingEffort, result[0].HighestCategory);
    }
}
